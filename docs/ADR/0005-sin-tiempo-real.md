# ADR 0005 — Sin tiempo real

**Estado**: aceptado.

## Contexto

Dados compartidos y tracker de iniciativa en vivo requerirían WebSockets (SignalR) y manejo de
conexión en el cliente. El usuario prefiere dados privados.

## Decisión

No hay canal de tiempo real. Los dados se tiran en el móvil y el historial se guarda localmente.
Las pantallas se refrescan al abrirlas o con "deslizar para actualizar".

## Consecuencias

- Un solo proceso API sin estado de conexión; escalado y despliegue más simples.
- Si en el futuro se quiere iniciativa compartida, se añade SignalR como módulo aparte.
