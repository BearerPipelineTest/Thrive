﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Newtonsoft.Json;
using File = Godot.File;

/// <summary>
///   Contains definitions for global game configuration like Compounds, Organelles etc.
/// </summary>
public class SimulationParameters : Node
{
    public const string AUTO_EVO_CONFIGURATION_NAME = "AutoEvoConfiguration";

    private static SimulationParameters? instance;

    private Dictionary<string, MembraneType> membranes = null!;
    private Dictionary<string, Background> backgrounds = null!;
    private Dictionary<string, Biome> biomes = null!;
    private Dictionary<string, BioProcess> bioProcesses = null!;
    private Dictionary<string, Compound> compounds = null!;
    private Dictionary<string, OrganelleDefinition> organelles = null!;
    private Dictionary<string, Enzyme> enzymes = null!;
    private Dictionary<string, MusicCategory> musicCategories = null!;
    private Dictionary<string, HelpTexts> helpTexts = null!;
    private PredefinedAutoEvoConfiguration autoEvoConfiguration = null!;
    private List<NamedInputGroup> inputGroups = null!;
    private Dictionary<string, Gallery> gallery = null!;
    private TranslationsInfo translationsInfo = null!;
    private GameCredits gameCredits = null!;
    private Dictionary<string, DifficultyPreset> difficultyPresets = null!;

    // These are for mutations to be able to randomly pick items in a weighted manner
    private List<OrganelleDefinition> prokaryoticOrganelles = null!;
    private float prokaryoticOrganellesTotalChance;
    private List<OrganelleDefinition> eukaryoticOrganelles = null!;
    private float eukaryoticOrganellesChance;

    private List<Compound>? cachedCloudCompounds;
    private List<Enzyme>? cachedDigestiveEnzymes;

    public static SimulationParameters Instance => instance ?? throw new InstanceNotLoadedYetException();

    public IEnumerable<NamedInputGroup> InputGroups => inputGroups;

    public IAutoEvoConfiguration AutoEvoConfiguration => autoEvoConfiguration;

    public NameGenerator NameGenerator { get; private set; } = null!;
    public PatchMapNameGenerator PatchMapNameGenerator { get; private set; } = null!;

    /// <summary>
    ///   Loads the simulation configuration parameters from JSON files
    /// </summary>
    /// <remarks>
    ///   This is now loaded in _Ready as otherwise the <see cref="ModLoader"/>'s _Ready would run after simulation
    ///   parameters are loaded causing some data that might want to be overridden by mods to be loaded too early.
    /// </remarks>
    public override void _Ready()
    {
        base._Ready();

        // Compounds are referenced by the other json files so it is loaded first and instance is assigned here
        instance = this;

        // Loading compounds and enzymes needs a custom JSON deserializer that can load their respective objects, but
        // the loader can't always be active because that breaks saving
        {
            var deserializers = new JsonConverter[] { new CompoundLoader(null), new EnzymeLoader(null) };

            compounds = LoadRegistry<Compound>(
                "res://simulation_parameters/microbe_stage/compounds.json", deserializers);
            enzymes = LoadRegistry<Enzyme>("res://simulation_parameters/microbe_stage/enzymes.json", deserializers);
        }

        membranes = LoadRegistry<MembraneType>("res://simulation_parameters/microbe_stage/membranes.json");
        backgrounds = LoadRegistry<Background>("res://simulation_parameters/microbe_stage/backgrounds.json");
        biomes = LoadRegistry<Biome>("res://simulation_parameters/microbe_stage/biomes.json");
        bioProcesses = LoadRegistry<BioProcess>("res://simulation_parameters/microbe_stage/bio_processes.json");
        organelles = LoadRegistry<OrganelleDefinition>("res://simulation_parameters/microbe_stage/organelles.json");

        NameGenerator = LoadDirectObject<NameGenerator>(
            "res://simulation_parameters/microbe_stage/species_names.json");

        musicCategories = LoadRegistry<MusicCategory>("res://simulation_parameters/common/music_tracks.json");

        helpTexts = LoadRegistry<HelpTexts>("res://simulation_parameters/common/help_texts.json");

        inputGroups = LoadListRegistry<NamedInputGroup>("res://simulation_parameters/common/input_options.json");

        autoEvoConfiguration =
            LoadDirectObject<PredefinedAutoEvoConfiguration>(
                "res://simulation_parameters/common/auto-evo_parameters.json");

        gallery = LoadRegistry<Gallery>("res://simulation_parameters/common/gallery.json");

        translationsInfo =
            LoadDirectObject<TranslationsInfo>("res://simulation_parameters/common/translations_info.json");

        gameCredits =
            LoadDirectObject<GameCredits>("res://simulation_parameters/common/credits.json");

        difficultyPresets =
            LoadRegistry<DifficultyPreset>("res://simulation_parameters/common/difficulty_presets.json");

        PatchMapNameGenerator = LoadDirectObject<PatchMapNameGenerator>(
            "res://simulation_parameters/microbe_stage/patch_syllables.json");

        GD.Print("SimulationParameters loading ended");

        CheckForInvalidValues();
        ResolveValueRelationships();

        // Apply translations here to ensure that initial translations are correct when the game starts.
        // This is done this way to allow StartupActions to run before SimulationParameters are loaded
        ApplyTranslations();

        GD.Print("SimulationParameters are good");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTranslationChanged)
        {
            ApplyTranslations();
        }
    }

    public OrganelleDefinition GetOrganelleType(string name)
    {
        return organelles[name];
    }

    /// <summary>
    ///   Returns all organelles
    /// </summary>
    public IEnumerable<OrganelleDefinition> GetAllOrganelles()
    {
        return organelles.Values;
    }

    public bool DoesOrganelleExist(string name)
    {
        return organelles.ContainsKey(name);
    }

    public MembraneType GetMembrane(string name)
    {
        return membranes[name];
    }

    public IEnumerable<MembraneType> GetAllMembranes()
    {
        return membranes.Values;
    }

    public bool DoesMembraneExist(string name)
    {
        return membranes.ContainsKey(name);
    }

    public Background GetBackground(string name)
    {
        return backgrounds[name];
    }

    public Biome GetBiome(string name)
    {
        return biomes[name];
    }

    public BioProcess GetBioProcess(string name)
    {
        return bioProcesses[name];
    }

    public Compound GetCompound(string name)
    {
        return compounds[name];
    }

    public Dictionary<string, Compound> GetAllCompounds()
    {
        return compounds;
    }

    public bool DoesCompoundExist(string name)
    {
        return compounds.ContainsKey(name);
    }

    public Enzyme GetEnzyme(string name)
    {
        return enzymes[name];
    }

    public IEnumerable<Enzyme> GetAllEnzymes()
    {
        return enzymes.Values;
    }

    public bool DoesEnzymeExist(string name)
    {
        return enzymes.ContainsKey(name);
    }

    /// <summary>
    ///   Returns all compounds that are clouds
    /// </summary>
    public List<Compound> GetCloudCompounds()
    {
        return cachedCloudCompounds ??= ComputeCloudCompounds();
    }

    public List<Enzyme> GetHydrolyticEnzymes()
    {
        return cachedDigestiveEnzymes ??= ComputeHydrolyticEnzymes();
    }

    public Dictionary<string, MusicCategory> GetMusicCategories()
    {
        return musicCategories;
    }

    public HelpTexts GetHelpTexts(string name)
    {
        return helpTexts[name];
    }

    public Dictionary<string, Gallery> GetGalleries()
    {
        return gallery;
    }

    public Gallery GetGallery(string name)
    {
        return gallery[name];
    }

    public bool DoesGalleryExist(string name)
    {
        return gallery.ContainsKey(name);
    }

    public TranslationsInfo GetTranslationsInfo()
    {
        return translationsInfo;
    }

    public GameCredits GetCredits()
    {
        return gameCredits;
    }

    public DifficultyPreset GetDifficultyPreset(string name)
    {
        return difficultyPresets[name];
    }

    public DifficultyPreset GetDifficultyPresetByIndex(int index)
    {
        return difficultyPresets.Values.First(p => p.Index == index);
    }

    public IEnumerable<DifficultyPreset> GetAllDifficultyPresets()
    {
        return difficultyPresets.Values;
    }

    public OrganelleDefinition GetRandomProkaryoticOrganelle(Random random, bool lawkOnly)
    {
        float valueLeft = random.Next(0.0f, prokaryoticOrganellesTotalChance);

        // Filter to only LAWK organelles if necessary
        IEnumerable<OrganelleDefinition> usedOrganelles = prokaryoticOrganelles;
        if (lawkOnly)
            usedOrganelles = usedOrganelles.Where(o => o.LAWK);

        OrganelleDefinition? chosenOrganelle = null;
        foreach (var organelle in usedOrganelles)
        {
            chosenOrganelle = organelle;
            valueLeft -= organelle.ProkaryoteChance;

            if (valueLeft <= 0.00001f)
                return chosenOrganelle;
        }

        if (chosenOrganelle == null)
            throw new InvalidOperationException("No organelle chosen to add");

        return chosenOrganelle;
    }

    public OrganelleDefinition GetRandomEukaryoticOrganelle(Random random, bool lawkOnly)
    {
        float valueLeft = random.Next(0.0f, eukaryoticOrganellesChance);

        // Filter to only LAWK organelles if necessary
        IEnumerable<OrganelleDefinition> usedOrganelles = eukaryoticOrganelles;
        if (lawkOnly)
            usedOrganelles = usedOrganelles.Where(o => o.LAWK);

        OrganelleDefinition? chosenOrganelle = null;
        foreach (var organelle in usedOrganelles)
        {
            chosenOrganelle = organelle;
            valueLeft -= organelle.ChanceToCreate;

            if (valueLeft <= 0.00001f)
                return chosenOrganelle;
        }

        if (chosenOrganelle == null)
            throw new InvalidOperationException("No organelle chosen to add");

        return chosenOrganelle;
    }

    public PatchMapNameGenerator GetPatchMapNameGenerator()
    {
        return PatchMapNameGenerator;
    }

    /// <summary>
    ///   Applies translations to all registry loaded types. Called whenever the locale is changed
    /// </summary>
    public void ApplyTranslations()
    {
        ApplyRegistryObjectTranslations(membranes);
        ApplyRegistryObjectTranslations(backgrounds);
        ApplyRegistryObjectTranslations(biomes);
        ApplyRegistryObjectTranslations(bioProcesses);
        ApplyRegistryObjectTranslations(compounds);
        ApplyRegistryObjectTranslations(organelles);
        ApplyRegistryObjectTranslations(enzymes);
        ApplyRegistryObjectTranslations(musicCategories);
        ApplyRegistryObjectTranslations(helpTexts);
        ApplyRegistryObjectTranslations(inputGroups);
        ApplyRegistryObjectTranslations(gallery);
        ApplyRegistryObjectTranslations(difficultyPresets);
    }

    private static void CheckRegistryType<T>(Dictionary<string, T> registry)
        where T : class, IRegistryType
    {
        foreach (var entry in registry)
        {
            entry.Value.InternalName = entry.Key;
            entry.Value.Check(entry.Key);
        }
    }

    private static void CheckRegistryType<T>(IEnumerable<T> registry)
        where T : class, IRegistryType
    {
        foreach (var entry in registry)
        {
            entry.Check(string.Empty);

            if (string.IsNullOrEmpty(entry.InternalName))
                throw new Exception("registry list type should set internal name in Check");
        }
    }

    private static void ApplyRegistryObjectTranslations<T>(Dictionary<string, T> registry)
        where T : class, IRegistryType
    {
        foreach (var entry in registry)
        {
            entry.Value.ApplyTranslations();
        }
    }

    private static void ApplyRegistryObjectTranslations<T>(IEnumerable<T> registry)
        where T : class, IRegistryType
    {
        foreach (var entry in registry)
        {
            entry.ApplyTranslations();
        }
    }

    private static string ReadJSONFile(string path)
    {
        using var file = new File();
        file.Open(path, File.ModeFlags.Read);
        var result = file.GetAsText();

        // This might be completely unnecessary
        file.Close();

        return result;
    }

    private Dictionary<string, T> LoadRegistry<T>(string path, JsonConverter[]? extraConverters = null)
    {
        extraConverters ??= Array.Empty<JsonConverter>();

        var result = JsonConvert.DeserializeObject<Dictionary<string, T>>(ReadJSONFile(path), extraConverters);

        if (result == null)
            throw new InvalidDataException("Could not load a registry from file: " + path);

        GD.Print($"Loaded registry for {typeof(T)} with {result.Count} items");
        return result;
    }

    private List<T> LoadListRegistry<T>(string path, JsonConverter[]? extraConverters = null)
    {
        extraConverters ??= Array.Empty<JsonConverter>();

        var result = JsonConvert.DeserializeObject<List<T>>(ReadJSONFile(path), extraConverters);

        if (result == null)
            throw new InvalidDataException("Could not load a registry from file: " + path);

        GD.Print($"Loaded registry for {typeof(T)} with {result.Count} items");
        return result;
    }

    private T LoadDirectObject<T>(string path, JsonConverter[]? extraConverters = null)
        where T : class
    {
        extraConverters ??= Array.Empty<JsonConverter>();

        var result = JsonConvert.DeserializeObject<T>(ReadJSONFile(path), extraConverters);

        if (result == null)
            throw new InvalidDataException("Could not load a registry from file: " + path);

        return result;
    }

    private void CheckForInvalidValues()
    {
        CheckRegistryType(membranes);
        CheckRegistryType(backgrounds);
        CheckRegistryType(biomes);
        CheckRegistryType(bioProcesses);
        CheckRegistryType(compounds);
        CheckRegistryType(organelles);
        CheckRegistryType(enzymes);
        CheckRegistryType(musicCategories);
        CheckRegistryType(helpTexts);
        CheckRegistryType(inputGroups);
        CheckRegistryType(gallery);
        CheckRegistryType(difficultyPresets);

        NameGenerator.Check(string.Empty);
        PatchMapNameGenerator.Check(string.Empty);
        autoEvoConfiguration.Check(string.Empty);
        autoEvoConfiguration.InternalName = AUTO_EVO_CONFIGURATION_NAME;
        translationsInfo.Check(string.Empty);
        gameCredits.Check(string.Empty);
    }

    private void ResolveValueRelationships()
    {
        foreach (var entry in organelles)
        {
            entry.Value.Resolve(this);
        }

        foreach (var entry in biomes)
        {
            entry.Value.Resolve(this);
        }

        foreach (var entry in backgrounds)
        {
            entry.Value.Resolve(this);
        }

        foreach (var entry in membranes)
        {
            entry.Value.Resolve();
        }

        foreach (var entry in compounds)
        {
            entry.Value.Resolve();
        }

        foreach (var entry in gallery)
        {
            entry.Value.Resolve();
        }

        NameGenerator.Resolve(this);

        BuildOrganelleChances();

        // TODO: there could also be a check for making sure
        // non-existent compounds, processes etc. are not used
    }

    private void BuildOrganelleChances()
    {
        prokaryoticOrganelles = new List<OrganelleDefinition>();
        eukaryoticOrganelles = new List<OrganelleDefinition>();
        prokaryoticOrganellesTotalChance = 0.0f;
        eukaryoticOrganellesChance = 0.0f;

        foreach (var entry in organelles)
        {
            var organelle = entry.Value;

            if (organelle.ChanceToCreate > 0)
            {
                eukaryoticOrganelles.Add(organelle);
                eukaryoticOrganellesChance += organelle.ChanceToCreate;
            }

            if (organelle.ProkaryoteChance > 0)
            {
                prokaryoticOrganelles.Add(organelle);
                prokaryoticOrganellesTotalChance += organelle.ChanceToCreate;
            }
        }
    }

    private List<Compound> ComputeCloudCompounds()
    {
        return compounds.Where(p => p.Value.IsCloud).Select(p => p.Value).ToList();
    }

    private List<Enzyme> ComputeHydrolyticEnzymes()
    {
        return enzymes.Where(e => e.Value.Property == Enzyme.EnzymeProperty.Hydrolytic).Select(e => e.Value).ToList();
    }
}
