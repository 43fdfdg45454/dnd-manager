# ADR 0006 — Tiempo real para eventos de campaña con SignalR

**Estado**: aceptado. Reemplaza al ADR 0005.

## Contexto

En la mesa, el DM necesita que lo que hace en su vista (forzar un descanso, aplicar daño o un
estado, abrir una tienda, enviar un mensaje secreto a un personaje) aparezca en el móvil del
jugador sin que este tenga que refrescar. El ADR 0005 descartó el tiempo real para simplificar el
despliegue; el usuario pidió explícitamente SignalR o equivalente.

## Decisión

- La API expone un hub SignalR en `/hubs/campaign` (paquete incluido en ASP.NET Core; sin
  dependencias externas). La autenticación es el mismo JWT de la API, pasado como `access_token`
  en la query solo para esa ruta.
- El hub solo **notifica**: cada evento lleva tipo e identificadores (`character.updated`,
  `party.rest`, `message.received`, `shop.updated`, `changeRequest.updated`, `session.updated`).
  Nunca lleva datos de la hoja: el cliente vuelve a pedir por HTTP lo que tiene permiso de ver, así
  las reglas de visibilidad siguen viviendo en los endpoints.
- Grupos: `campaign:{id}` (miembros que han hecho `JoinCampaign`, comprobada la pertenencia) y
  `user:{id}` (mensajes dirigidos).
- Los dados siguen siendo privados (ADR 0005 en ese punto se mantiene). No hay iniciativa
  compartida ni estado en memoria más allá de los grupos de conexión.
- Transporte: WebSockets con caída automática a SSE o long polling. El reverse proxy del operador
  debe reenviar `Upgrade`/`Connection` (documentado en `deploy/README.md`).
- Cliente: `signalr_netcore`; conecta mientras una campaña está abierta y se detiene sin red. Los
  eventos invalidan los proveedores Riverpod afectados.

## Consecuencias

- Un solo proceso API sin backplane: suficiente para una instancia doméstica. Si algún día hay
  varias réplicas, hace falta un backplane (Redis) o sesiones pegajosas.
- Los tests de integración cubren el hub con `HubConnection` contra el `WebApplicationFactory`.
