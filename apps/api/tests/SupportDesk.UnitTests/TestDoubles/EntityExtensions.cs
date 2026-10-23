using SupportDesk.Domain.Common;

namespace SupportDesk.UnitTests.TestDoubles;

public static class EntityExtensions
{
    /// <summary>
    /// Entity ids are assigned by the database, so a test that needs distinct ids sets one here.
    /// </summary>
    public static T WithId<T>(this T entity, int id) where T : Entity
    {
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);

        return entity;
    }
}
