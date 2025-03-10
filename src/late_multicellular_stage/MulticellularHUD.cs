﻿using System;
using System.Collections.Generic;
using Godot;
using Newtonsoft.Json;

/// <summary>
///   Manages the multicellular HUD scene
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public class MulticellularHUD : StageHUDBase<MulticellularStage>
{
    [Export]
    public NodePath PrototypeInfoPopupPath = null!;

    [Export]
    public NodePath AwareButtonPath = null!;

    [Export]
    public NodePath AwakenButtonPath = null!;

    private CustomDialog prototypeInfoPopup = null!;
    private Button awareButton = null!;
    private Button awakenButton = null!;

    // These signals need to be copied to inheriting classes for Godot editor to pick them up
    [Signal]
    public new delegate void OnOpenMenu();

    [Signal]
    public new delegate void OnOpenMenuToHelp();

    protected override string? UnPauseHelpText => null;

    public override void _Ready()
    {
        base._Ready();

        prototypeInfoPopup = GetNode<CustomDialog>(PrototypeInfoPopupPath);
        awareButton = GetNode<Button>(AwareButtonPath);
        awakenButton = GetNode<Button>(AwakenButtonPath);

        // TODO: remove this once gameplay is added
        prototypeInfoPopup.PopupCenteredShrink();
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        if (stage == null)
            return;

        if (stage.HasPlayer)
        {
            UpdateAwareButton(stage.Player!);
            UpdateAwakenButton(stage.Player!);
        }
        else
        {
            awareButton.Visible = false;
            awakenButton.Visible = false;
        }
    }

    protected override void ReadPlayerHitpoints(out float hp, out float maxHP)
    {
        // TODO: player hitpoints
        hp = 100;
        maxHP = 100;
    }

    protected override CompoundBag GetPlayerUsefulCompounds()
    {
        return stage!.Player!.ProcessCompoundStorage;
    }

    protected override Func<Compound, bool> GetIsUsefulCheck()
    {
        var bag = stage!.Player!.ProcessCompoundStorage;
        return c => bag.IsUseful(c);
    }

    protected override bool SpecialHandleBar(ProgressBar bar)
    {
        return false;
    }

    protected override bool ShouldShowAgentsPanel()
    {
        throw new NotImplementedException();
    }

    protected override void CalculatePlayerReproductionProgress(out Dictionary<Compound, float> gatheredCompounds,
        out Dictionary<Compound, float> totalNeededCompounds)
    {
        // TODO: reproduction process for multicellular
        gatheredCompounds = new Dictionary<Compound, float>
        {
            { ammonia, 1 },
            { phosphates, 1 },
        };
        totalNeededCompounds = new Dictionary<Compound, float>
        {
            { ammonia, 1 },
            { phosphates, 1 },
        };
    }

    protected override ICompoundStorage GetPlayerStorage()
    {
        return stage!.Player!.ProcessCompoundStorage;
    }

    protected override ProcessStatistics? GetPlayerProcessStatistics()
    {
        return stage!.Player!.ProcessStatistics;
    }

    protected override IEnumerable<(bool Player, Species Species)> GetHoveredSpecies()
    {
        // TODO: implement nearby species
        return Array.Empty<(bool Player, Species Species)>();
    }

    protected override IReadOnlyDictionary<Compound, float> GetHoveredCompounds()
    {
        // TODO: implement nearby compounds
        return new Dictionary<Compound, float>();
    }

    protected override string GetMouseHoverCoordinateText()
    {
        // TODO: get world point for cursor
        throw new NotImplementedException();
    }

    protected override void UpdateAbilitiesHotBar()
    {
    }

    private void UpdateAwareButton(MulticellularCreature player)
    {
        // TODO: condition
        awakenButton.Visible = true;
        awakenButton.Disabled = true;
    }

    private void OnBecomeAwarePressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // TODO: aware stage not done yet
        ToolTipManager.Instance.ShowPopup(TranslationServer.Translate("TO_BE_IMPLEMENTED"), 2.5f);
    }

    private void OnAwakenPressed()
    {
        GUICommon.Instance.PlayButtonPressSound();

        // TODO: awakening stage not done yet
        ToolTipManager.Instance.ShowPopup(TranslationServer.Translate("TO_BE_IMPLEMENTED"), 2.5f);
    }

    private void UpdateAwakenButton(MulticellularCreature player)
    {
        // TODO: condition
        awakenButton.Visible = false;
    }
}
