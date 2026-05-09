namespace OpenGarrison.Core;

public enum CustomMapBuilderResourceKind
{
    GenericImage = 0,
    ParallaxLayer = 1,
    Foreground = 2,
    EntitySprite = 3,
}

public readonly record struct CustomMapBuilderResource(
    string Name,
    string SourcePath,
    CustomMapBuilderResourceKind Kind = CustomMapBuilderResourceKind.GenericImage)
{
    public CustomMapBuilderResource NormalizeForEditing()
    {
        return this with
        {
            Name = Name.Trim(),
            SourcePath = SourcePath.Trim(),
        };
    }
}
