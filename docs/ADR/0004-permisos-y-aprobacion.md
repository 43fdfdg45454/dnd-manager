# ADR 0004 — Permisos: ítems de la campaña y aprobación del DM

**Estado**: aceptado (con un supuesto a validar).

## Contexto

El usuario definió que los ítems de un personaje pertenecen a la campaña, que el jugador solo
los modifica mediante operaciones (compra y similares) y que el DM siempre aprueba las
modificaciones.

## Decisión

- `CharacterItem` referencia a la campaña; los ítems homebrew (`ItemTemplate` con `campaignId`)
  viven en la campaña.
- Operaciones permitidas al jugador sin aprobación: comprar/vender en tienda abierta, equipar,
  usar consumible, atunement, y el auto-seguimiento de combate (HP, slots, recursos, condiciones,
  concentración, rests).
- Cualquier otra modificación del personaje crea un `ChangeRequest` que el DM aprueba o rechaza.
  DM y Owner aplican cambios directamente.

## Supuesto

El auto-seguimiento de combate no requiere aprobación porque es llevar la cuenta, no cambiar la
hoja. Si el usuario quiere aprobación también ahí, se añaden esos tipos a `ChangeRequest`.
