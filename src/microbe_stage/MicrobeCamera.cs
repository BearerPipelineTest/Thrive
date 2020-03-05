using Godot;

public class MicrobeCamera : Camera
{
    private ShaderMaterial materialToUpdate;

    public Vector3 CursorWorldPos { get; private set; }

    public override void _Ready()
    {
        var material = GetNode<CSGMesh>("BackgroundPlane").Material;
        if (material == null)
        {
            GD.PrintErr("MicrobeCamera didn't find material to update");
            return;
        }

        materialToUpdate = (ShaderMaterial)material;

        CursorWorldPos = new Vector3(0, 0, 0);
    }

    /// <summary>
    ///   Updates camera pos
    /// </summary>
    public override void _Process(float delta)
    {
        var velocity = default(Vector3);

        if (Input.IsActionPressed("ui_right"))
        {
            velocity.x += 1;
        }

        if (Input.IsActionPressed("ui_left"))
        {
            velocity.x -= 1;
        }

        if (Input.IsActionPressed("ui_down"))
        {
            velocity.z += 1;
        }

        if (Input.IsActionPressed("ui_up"))
        {
            velocity.z -= 1;
        }

        velocity = velocity.Normalized() * 40.0f * delta;

        Translation = Translation + velocity;

        UpdateCursorWorldPos();
    }

    private void UpdateCursorWorldPos()
    {
        var worldPlane = new Plane(new Vector3(0, 1, 0), 0.0f);

        var mousePos = GetViewport().GetMousePosition();

        var intersection = worldPlane.IntersectRay(ProjectRayOrigin(mousePos),
            ProjectRayNormal(mousePos));

        if (intersection.HasValue)
        {
            CursorWorldPos = intersection.Value;
        }
    }
}