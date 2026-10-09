using Dnd.Application.Common;

namespace Dnd.Application.Catalog;

public static class CatalogErrors
{
    public static AppException ClassNotFound() => AppException.NotFound("Clase no encontrada.");

    public static AppException RaceNotFound() => AppException.NotFound("Raza no encontrada.");

    public static AppException SpellNotFound() => AppException.NotFound("Conjuro no encontrado.");

    public static AppException BeastNotFound() => AppException.NotFound("Bestia no encontrada.");

    public static AppException ItemNotFound() => AppException.NotFound("Objeto no encontrado.");

    public static AppException FeatureNotFound() => AppException.NotFound("Rasgo no encontrado.");

    public static AppException EquipmentCategoryNotFound() => AppException.NotFound("Categoría de equipo no encontrada.");
}
