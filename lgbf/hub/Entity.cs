namespace hub;

public record class ReadOnlyEntity(string Guid)
{
}
public record class Entity(Context Ctx)
{
}