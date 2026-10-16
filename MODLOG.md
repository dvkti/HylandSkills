# MODLOG: Hyland Skills (Schedule I)

## Entorno
- Juego: `E:\SteamLibrary\steamapps\common\Schedule I`. Versión 0.4.7f12, Unity 2022.3.62f2, IL2CPP x64. Steam AppID 3164500.
- Loader: MelonLoader 0.7.3, gestionado por Thunderstore Mod Manager.
  - Perfil: `%APPDATA%\Thunderstore Mod Manager\DataFolder\ScheduleI\profiles\Default` (Core::BasePath).
  - Lanzar desde Steam sin el perfil falla: `MelonLoader\net6` no existe en la carpeta del juego.
- Mods del perfil: S1API Forked 3.2.1 (ifBars) y **CrazyTeam SkillTree 3.4.1**, otro mod de árbol de habilidades. Puede chocar con este mod (salud máxima y otros), así que hay que desactivarlo para las pruebas.
- Ensamblados interop: `<perfil>\MelonLoader\Il2CppAssemblies`. Runtime de MelonLoader: `<perfil>\MelonLoader\net6`.
- SDK: .NET 8.0.426 instalado por usuario en `%LOCALAPPDATA%\dotnet-sdk`, porque winget necesitaba UAC.
- ilspycmd 8.2: hay que ejecutarlo con `DOTNET_ROOT="C:\Program Files\dotnet"` y `DOTNET_ROLL_FORWARD=Major`.
- Decompilado, fuera del repo: `E:\IA local\s1-decomp\`.
  - Carpetas `Assembly-CSharp`, `S1API` y `SkillTreeOther`.
  - Índice compacto de la API: `api_index.txt`.

## Ruta elegida
MelonLoader + Harmony sobre interop IL2CPP, más S1API para la app del celular (PhoneApp) y el guardado (Saveable).
Motivo: S1API ya resuelve el registro de apps del celular y el guardado dentro de la partida, y la comunidad lo usa.

## Datos del juego (verificados en el código)
- `ERank`: Street_Rat … Kingpin (11). `LevelManager.TIERS_PER_RANK`, `onRankUp(FullRank, FullRank)`.
- Hooks candidatos:
  - Cultivo: `Pot.YieldMultiplier`, `Pot.GrowSpeedMultiplier`, `Plant.QualityLevel`, `GrowContainer._moistureDrainPerHour`, `ShroomColony`.
  - Estaciones: `MixingStation.MixTimePerItem`, `Cauldron.COCA_LEAF_REQUIRED`, `DryingRack`.
  - Clientes: `Customer.EvaluateCounteroffer`, `GetSampleSuccess`, `ChangeAddiction`, `ProcessHandoverServerSide(totalPayment)`.
  - Dealers: `Dealer.MAX_CUSTOMERS`, `SalesCutPercentage`, `RobDealer.GetDealerToRob`, `Ambush.CanPlayerBeAmbushed`.
  - Dinero: `ATM.WeeklyDepositLimit` (static), `Business.LaunderCapacity`, `Employee.DailyWage`, `Employee.WorkSpeedController` (FloatStack).
  - Jugador: `PlayerHealth.MaxHealth` (static), `HealthRecoveryPerMinute`, `PlayerMovement.MoveSpeedMultiplierStack`, `PunchController.Min/MaxPunchDamage`.
  - Policía: `VisionCone.StateContainer.NoticeTimeMultiplier`, `PoliceOfficer.BodySearchChance`, `PlayerCrimeData` (búsqueda), `PenaltyHandler.*_FINE`, `CurfewManager.IsCurrentlyActive`.

## Registro
- 2026-10-15: recon, SDK, decompilado y diseño (DESIGN.md).
- 2026-10-15: verificación de pasivas en el juego: 9 arreglos y 2 ajustes (ver la tabla al final).
- 2026-10-16 (6): casino arreglado y probado en partida real. Los bonos de ganancia no se aplicaban: el juego calcula el premio con `GetWinAmount`, `GetPayout` y `GetNetBetMultiplier` inlineados, así que los patches solo funcionaban al llamarlos desde el bridge. Ahora el bono se paga como efectivo extra al terminar la jugada (`DisplayOutcome` en las tragamonedas, `RemoveLocalPlayerFromGame` en las mesas). La barra de apuesta de Blackjack y Ride the Bus quedaba en $0..$0 por el postfix de High Roller sobre los parámetros `out` de `GetBetLimits`; ahora High Roller sube el máximo de la barra en `CasinoGameBetPanel.Open`. Autor del mod: GetToWorkTylerPls.
- 2026-10-16 (5): rama completa = círculo de la categoría relleno con su color e icono blanco (sin el punto dorado). Diamante de maestría teñido de dorado (suave si se puede elegir, sólido si está elegida) con icono blanco y sombra. Etiqueta "Mastery" más grande, dorada y con un destello de cuatro puntas generado por código.
- 2026-10-16 (4): fuera la placa, la corona y los brillos de las maestrías (se sentían forzados). Ahora son solo los rombos grandes, unidos directamente al último nivel; al abrirse se rellenan con una talla de diamante procedural (facetas triangulares en grises, inspirada en un brillante) y la elegida queda clara, más grande y con el icono oscuro.
- 2026-10-16 (3): placa de maestría (marco dorado, cresta con corona donde converge el árbol, brillo, se ilumina al completar la rama); iconos de maestría en dorado; la elegida crece y emite un destello. Animaciones: líneas que se llenan desde la habilidad padre, pop al aprender y seleccionar, crecimiento al pasar por las categorías, fundido al cambiar de página. Reinicio pagado desde el banco ($5,000 → $75,000 tope). Sonidos nativos: clic de botón, blip de subida de rango, jingle de mezcla nueva (maestría), mensaje enviado (equipar), pago del cajero (reinicio). Los clips de pantallas no abiertas (rank-up) se leen del AudioSource porque su controlador aún no corrió Awake.
- 2026-10-16 (2): combos rediseñados (11 combinaban una habilidad con su propio requisito; ahora cada pareja está en caminos distintos, comprobado con un verificador). Líneas del árbol con el codo justo bajo el nodo padre (ya no se funden líneas de padres distintos) y tronco de maestrías desde los nodos del último nivel. Maestrías destacadas (banda dorada, "Choose 1", confirmación, candado en las otras dos). Puntos de rango al lado del icono. Menú nuevo de habilidades activas (asignar, quitar, cambiar tecla). Balance: casino sin beneficio garantizado (bonos bajos y reembolsos con tope de $1,000 al día), contraofertas por techo de precio (antes cualquier precio absurdo tenía un 24 % de aceptarse), Kickflip +5 %/rango (antes +20 %), Air Control ahora estabiliza la tabla en el aire, Street Surfer +15 %, Alchemist 35 % de mezclas con +25 %.
- 2026-10-16: puntos reequilibrados para 8 ramas (2 por tier = 100, 4 maestrías). UI: textos ~30 % más grandes, nodos e iconos más grandes, scroll en el árbol, óvalos cambiados por rectángulos redondeados, círculos en las sinergias y paneles planos al estilo de las apps nativas.

## Hechos verificados en el juego (2026-10-15)
- Casi todos los valores de ajuste son **const** (literal IL2CPP): `PlayerHealth.MaxHealth`, `HealthRecoveryPerMinute`, `ATM.WeeklyDepositLimit`, `Dealer.MAX_CUSTOMERS`, `Cauldron.COCA_LEAF_REQUIRED`, `DryingRack.DRY_MINS_PER_TIER`, `PlayerCrimeData.SEARCH_TIME_*`, `PoliceOfficer.BODY_SEARCH_CHANCE_DEFAULT`, `PenaltyHandler.*_FINE`, `Customer.*`, `LevelManager.TIERS_PER_RANK`, `CurfewManager.*`.
  - Escribirlos con interop **cierra el juego** sin dejar log. `Il2CppStatics.IsWritable` revisa `FIELD_ATTRIBUTE_LITERAL` (0x40) antes de escribir.
  - Sí se puede escribir: `ATM.WeeklyDepositSum`.
- Rutas que funcionan:
  - Campos de instancia: `Pot.GrowSpeedMultiplier`, `GrowContainer._moistureDrainPerHour`, `Business.LaunderCapacity`, `Employee.DailyWage`, `DealerNPCData.SalesCutPercentage`, `VisionCone.Attentiveness`, `LandVehicle.TopSpeed`, `StorableItemDefinition.BasePurchasePrice`.
  - FloatStacks con etiqueta: `PlayerMovement.MoveSpeedMultiplierStack`, `Employee.WorkSpeedController`.
  - Patches sobre `RpcLogic___*`: pago (`Customer.ProcessHandoverServerSide`), `TakeDamage`, `ReceiveImpact`. La adicción no: su `RpcLogic` está inlineado y se parchea el emisor `RpcWriter___Server_ChangeAddiction`.
- Estaciones: se escala `ChemistryCookOperation.Progress`, `OvenCookOperation.UpdateCookProgress` y `OnTimePass(ref minutes)`. En Cauldron y Mixer se usa un postfix sobre `OnMinPass` que ajusta el tiempo restante. Un acumulador fraccional por objeto evita perder minutos.
- La calidad de las plantas no se guarda (`PlantData`: SeedID, GrowthProgress, ActiveBuds). Por eso la calidad se aplica en `GetHarvestedProduct` y el rendimiento en `GrowthDone`, solo con el mundo ya cargado.
- UI nativa del celular:
  - Fondo #323232, panel #3C3C3C, tarjetas #202020, cabecera de 60 px con el color de la app y título OpenSans-Bold 24.
  - Fuentes TMP "OpenSans-Bold SDF" y "OpenSans-SemiBold SDF".
  - El sprite nativo "Rectangle_RoundedEdges" se deforma al escalarlo, así que se usa uno procedural.
  - Los textos TMP con `Overflow=Ellipsis` desaparecen si la línea no entra en altura.
- `um win drive`: para que Tab abra el celular hay que hacer clic en la ventana. La tecla de consola en teclado español es `0xDC`.
- Guardado: S1API escribe en `SaveGame_N/Modded/Saveables/SkillSaveable/HylandSkills.json`. Se probó guardar (comando F9 de debug), reiniciar y cargar: el estado volvió completo.
- Pruebas en el juego que pasaron:
  - App: árbol, aprender, maestría exclusiva, equipar, teclas configurables, ajustes del HUD y respec (cobró $1,500).
  - Activas: Adrenaline, Overdrive, Rush Hour, Hype Text (3 clientes pidieron tratos), Prune (falla bien sin planta) y Vanish (terminó "Investigating").
  - Sin excepciones en sesiones de unos 10 minutos.
- La partida de prueba SaveGame_3 se restauró desde el backup (`um backup restore schedule1-saves --clean`).

## Verificación de pasivas en el juego (2026-10-15)
Partida SaveGame_3 "Armstrong", restaurada después desde el backup (sin cambios respecto al backup).

**Método.** `Core/DebugBridge.cs` (solo activo con `DebugLogging = true`) lee órdenes de `UserData/HylandSkills.bridge.txt` y escribe en `UserData/HylandSkills.bridge.out`: comandos de la consola del juego, rangos de skills, lectura y escritura de campos, llamadas a métodos y constructores. Los patches escriben trazas `[trace]` con valor base → modificado (solo con `DebugLogging`), y `Core/DebugTraces.cs` mide el tiempo de aviso de la policía y los pasos del registro corporal.
Siempre que se pudo, el efecto se disparó por el camino del propio juego (botón del cajero, RPC del cliente, minijuego del registro, tick de las estaciones), no llamando al método parcheado. En IL2CPP un patch puede no ejecutarse si el juego tiene ese método inlineado en quien lo llama.

**Lecciones nuevas (IL2CPP):**
- Los métodos pequeños se inlinean en el código nativo y el patch no se ejecuta: `OvenCookOperation.UpdateCookProgress`, `GrowContainer.SetRemainingSoilUses`, `Customer.RpcLogic___ChangeAddiction` (dentro del lector del RPC) y la comprobación de límite del cajero. La solución es parchear al que llama (el tick o el emisor del RPC) o escribir el resultado después.
- Las sobrescrituras (`WeedPlant`/`CocaPlant.GetHarvestedProduct`, `MixingStationMk2.OnTimePass`) no pasan por el patch de la clase base: hay que parchear cada una.
- `ItemSlot.ChangeQuantity` dispara `OutputChanged`. Un patch que cambia la cantidad dentro de ese evento tiene que limpiar su estado antes, o entra en recursión y cierra el juego.

- Un patch de Harmony con parámetros por referencia (`ref` en un enum y un `int`, más `out __state`) sobre `SlotMachine.DisplayOutcome` cerró el juego al aplicarse (error interno de coreclr). Se usan prefijos simples que solo leen.
- Un postfix que escribe en los parámetros `out float` de `GetBetLimits` dejó la barra de apuesta en $0..$0. Mejor cambiar el objeto de UI que usa el resultado.

| Pasiva / sinergia | Estado | Medido en el juego |
|---|---|---|
| Green Thumb | Verificada (+ ajuste) | 12 cogollos → 13 (rango 1) → 14 (rango 3). Ajuste: el bonus a `Plant.YieldMultiplier` se acumulaba si `GrowthDone` se repetía; ahora se restaura tras la llamada |
| Green Sense / Heirloom / Prune (calidad) | **Arreglada** | `WeedPlant` y `CocaPlant` sobrescriben `GetHarvestedProduct` sin llamar a la base, así que el bonus nunca se aplicaba (102 % de probabilidad → Standard). Ahora se parchean las tres. Con 100 %: Standard → Premium, Premium → Heavenly; con 18 %, varias subidas en una cosecha |
| Organic Methods | Verificada | Maceta con PGR: rendimiento ×1,5 → ×1,35 (−10 %). Sin potenciadores: +0,10 de probabilidad de calidad en cada cosecha |
| Seed Saver | Verificada | Al cosechar la planta entera volvió 1 semilla OG Kush |
| Living Soil | **Arreglada** | `SetRemainingSoilUses` está inlineado: la tierra seguía gastándose (1 → 0, tierra borrada). Ahora se protege en `Pot.OnPlantFullyHarvested`: usos 2 → 2, tierra intacta |
| Deep Roots | Verificada | Humedad perdida por hora: 0,436 → 0,289 (−34 %; esperado −36 %) |
| Steady Hands | **Arreglada** (horno) / verificada (química) | El horno ignoraba el skill (`UpdateCookProgress` inlineado). Ahora el tick suma los minutos extra. Horno: +19 → +23 en 20 min (+21 %). Chemistry Station: +30 → +35 en 30 min (+17 %). Esperado +18 % |
| Mixologist | **Arreglada** | La mezcladora Mk2 escalaba el minuto dos veces (+55 % en lugar de +30 %). Ahora un único patrón cubre a la vez el tick y el salto de tiempo. Medido: +19 → +25 en 20 min (+32 %) |
| Precise Measures (calidad) | Verificada | Con más del 100 %, el horno terminado por el flujo normal dio meth ×50 Premium a partir de ingrediente Standard |
| Ingredient Saver | Verificada | Al terminar la mezcla volvieron las 10 unidades de Viagor a la mezcladora |
| Alchemist | **Arreglada** | El objeto de salida aparece después de `TryCreateOutputItems`, así que el bonus nunca se sumaba. Ahora se aplica en `OutputChanged`: salida 10 → 12 |
| Pago de tratos (Premium Brand + Kingmaker) | Verificada | Trato real de un dealer: 507 → 634 (×1,25). El dealer recibió 538,9, que es el 85 % del pago aumentado |
| Complex Formulas | Verificada | Producto con 3 o más efectos: ×1,12 |
| Back Alley (sinergia) | Verificada | Trato durante el toque de queda: ×1,10 |
| VIP Host | Verificada | Multiplicador de precio de Special Customer 1,25 → 1,5625 (+25 %) |
| Silver Tongue | Verificada | Contraoferta de $5 000 por el flujo nativo (`SendCounteroffer`): rechazada sin el skill, aceptada con él |
| Respect (sinergia) | Verificada | Bonus de contraoferta 0,24 → 0,34 |
| Free Taste | Verificada | Muestra real (el cliente la consume): probabilidad 0 → 0,24 (rango 3) |
| People Person | Verificada | Relación +0,10 → +0,14 (rango 2) |
| Addiction Economics | **Arreglada** | El cuerpo de `RpcLogic___ChangeAddiction` está inlineado en el lector del RPC y la adicción no subía más. Ahora se parchea el emisor: +0,10 → +0,14. La penalización funciona: relación −0,10 → −0,20 |
| Dealer Network / Kingmaker (comisión) | Verificada | Comisión 20 % → 15 % (Kingmaker) → 9 % (+ Dealer Network 3) |
| Bookkeeper / Tycoon (capacidad) | Verificadas | Capacidad +30 % / +25 %. El `appliedLaunderLimit` del juego también sube (Laundromat 0 → 600 libres) |
| Tycoon (velocidad) | **Arreglada** | El contador del minuto extra era compartido y se repartía entre los negocios (+10 % medido). Ahora es por negocio: +50 min en 40 min en los dos (+25 %). Limitación: no acelera los saltos de tiempo al dormir |
| Legit Fronts | Verificada | Al pasar la medianoche: +$240 online (2 negocios × $120) |
| Bulk Buyer | Verificada | Precio del listado en la tienda $100 → $88 (rango 3) |
| Payroll / Overtime / Union Boss / Head Botanist (sueldos) | Verificadas | 300 → 290 (Payroll 2 + Overtime). Químico 270, botánico 216 (Union + Head Botanist) |
| Velocidad de empleados | Verificada | `Employee.CurrentWorkSpeed` 1 → 1,2 (Overtime) y 1,495 (botánico con Union + Head Botanist) |
| Industrial Scale (sinergia) | Verificada | Químicos 1,2; botánicos 1,0 |
| ATM Connections / Offshore Account | **Arreglada** | La pantalla del cajero limita cada importe con la constante 10 000 en línea, así que solo cambiaba la etiqueta. Ahora, con la pantalla abierta, se resta el bonus de `ATM.WeeklyDepositSum` (que sí se puede escribir) y se repone al cerrar. Con la suma ya en 10 000 se depositaron $1 000 (suma → 11 000). Offshore: límite 20 000 |
| Low Profile | Verificada | `Attentiveness` 1 → 0,769 (rango 3). El juego usa ese valor: con 0,25, el paso de medio a pleno aviso tardó 1,4 s frente a 0,4 s |
| Night Owl | Verificada | Con toque de queda: `Attentiveness` 1 → 0,769 (rango 3) |
| Hidden Pockets | **Arreglada** | El registro es un minijuego (`BodySearchScreen`) y el patch anterior no decidía el resultado: con 156 % hubo multa igual. Ahora, si sale la tirada, `ItemDetected` no detecta nada y `Close` cierra limpio → `SearchClean`, se conserva la droga y no hay multa. Sin el skill: `SearchFail` y multa de $50 |
| Ghost Protocol | Verificada | De día, `BeginBodySearch` no llega a empezar el registro |
| Clean Hands (multas) | Verificada (+ ajuste) | Multa base $50 → se cobran $100. Ajuste: el aviso de la policía ahora muestra la multa real ($100,00) |
| Offshore Accounts (sinergia) | Verificada | Multa $50 → $25 netos |
| Phantom | Verificada | El juego llama a `GetSearchTime` durante la persecución: 60 s → 36 s |
| Thick Skin / Bruiser / Juggernaut | Verificadas | 20 de daño → 14,45 (Thick Skin 3 + Bruiser) y → 16 (Juggernaut) |
| Home Turf (sinergia) | Verificada | Dentro de una propiedad propia: 20 → 11,9 (×0,85 × 0,7) |
| Second Wind | Verificada | Regeneración 0,5 → 1,1 de vida por minuto de juego (rango 3, +120 %) |
| Heavy Hands / Gunslinger | Verificadas | Impacto enviado al NPC por el RPC: puñetazo 10 → 13,6; bala 10 → 12,5 |
| Steady Aim | Verificada | Dispersión de la M1911 4/12 → 2,8/8,4 (rango 2) |
| Notorious | Verificada | `CanPlayerBeAmbushed`: 14 de 30 comprobaciones bloqueadas (~47 %; objetivo 50 %) |
| Protection Racket | Verificada | `RobDealer.Activate` del juego: sin el skill, Brad perdió $8 375 → $6 516; con el skill no hay objetivo |
| Farm to Lab (sinergia) | Verificada | Sin Precise Measures: 19 de 150 tiradas del horno subieron (12,7 %; objetivo 10 %) |
| Lucky Charm / The House (tragamonedas) | **Arreglada** (2026-10-16) | `GetWinAmount` está inlineado en la corrutina del giro. Ahora se paga en `DisplayOutcome`: Mini Win con apuesta de $100, $200 → +$12 extra (9 % + 3 %), efectivo real |
| Lucky Seven | **Arreglada** (2026-10-16) | `EvaluateOutcome` tampoco pasaba por el patch. Ahora un giro perdedor pagó $212 (Mini Win con bono) |
| Card Sense (Blackjack) | **Arreglada** (2026-10-16) | Victoria con $2,000: el juego pagó $4,000 y el mod +$150 (4,5 % + 3 % de The House) |
| Bus Rider (Ride the Bus) | **Arreglada** (2026-10-16) | x2 con $1,000: +$90; x20: +$1,710 (6 % + 3 %) |
| High Roller | **Arreglada** (2026-10-16) | La barra quedaba en $0..$0. Ahora $10..$1,000 → $10..$2,000 (rango 2), el juego aceptó la apuesta de $2,000 y no se acumula al reabrir la mesa |
| Loss Insurance | Verificada (2026-10-16) | Rondas perdidas de $1,000 en Ride the Bus: −$700 netos (30 % devuelto) |
| Laundered Luck | Verificada (usuario) | Mueve parte del premio al banco |
| Juggernaut (no tambalear), Light Steps, Wheelman, Street Legend (propinas), Premium Brand (frecuencia de pedidos), Volatile Chemistry, Crystal Purity, Coca Refiner, Mycologist | Sin medir | En la partida no hay Cauldron ni colonias de hongos. El resto necesita conducir, caminar o hacer un trato a mano. Crystal Purity y Volatile Chemistry usan el mismo código que Precise Measures y Steady Hands, que sí se verificaron |

**Endurecimiento:** los campos que el mod escala (comisión de dealers, capacidad de lavado, sueldos, velocidad de crecimiento, drenaje de agua, atención de la policía y velocidad del vehículo) usan ahora `TrackedBase`. Si el propio juego cambia el valor, ese valor pasa a ser la nueva base en lugar de pisarlo con uno viejo.

## v2 en curso (2026-10-16): pulido de UI + 2 ramas nuevas
Lo que pidió el usuario:
- Íconos minimalistas.
- UI más limpia.
- Árbol estilo Cyberpunk 2077, con prerrequisitos y ramificaciones.
- Ramas nuevas de Gambling (casino) y Mobility (doble salto, skate).
- Skills para los clientes especiales (Hippies, Bikers, Party Bus) y para las tiendas.
- Dos menús: octágono de categorías → árbol de la rama.
- Temporizadores claros (activa / duración / cooldown).
- Combos entre skills relacionadas que cambian la mecánica.
- Al terminar: guardar el progreso y apagar el PC.

Estado:
- [x] `SkillDefs.cs` reescrito:
  - 8 ramas en el octágono: Cultivation, Chemistry, Empire, Gambling, Hustle, Shadows, Mobility, Muscle. Cada una cuesta 24 puntos (verificado con script).
  - Niveles 0-4 abiertos por puntos gastados en la rama ([0,3,7,12,17]); maestrías en el nivel 5 (24 puntos + 1 MP).
  - `Parents` define el prerrequisito.
  - 23 combos (`ComboDef`) y 8 sinergias en las aristas del octágono.
- [x] Íconos v2: Lucide (ISC, `Assets/LICENSE-lucide.txt`) en blanco, que el juego tiñe. Script: `tools/icons/make_icons_v2.py <lucide-static/icons>`. Se descarga con `npm lucide-static@0.468.0`.
- [ ] Estado: `CanLearn` con padres y niveles; combos.
- [ ] UI: menú octágono + árbol de la rama.
- [ ] HUD de temporizadores claro.
- [ ] Efectos nuevos:
  - Gambling: Blackjack `GetPayout`, `GetBetLimits`, `DrawCard`; Slots `GetWinAmount`, `StartSpin`; RTB `GetNetBetMultiplier`.
  - Mobility: `PlayerMovement.movementY` / `IsGrounded`; ajustes del skateboard.
  - Especiales: `GetBuyQuantityForPlayerRank`, `bonusPerDayUnseen`.
  - Tiendas: `DeliveryShop.GetDeliveryTime` / `GetDeliveryCost`; pawn shop `StartNegotiation` / `EvaluateCounter`.
  - Combos.
- [ ] Pruebas en el juego, empaquetado, apagar el PC.
- [x] (v2) Estado con padres/niveles/combos; UI nueva (`OverviewPage` octágono, `BranchPage` árbol con codos, puertas de nivel, líneas de combo, panel de detalles con COMBO); HUD de tarjetas (READY / ACTIVE x left / COOLDOWN x) con barra.
- [x] (v2) Efectos nuevos: `GamblingModule` (blackjack/RTB/slots, límites, seguro, lavado de ganancias, Peek, Lucky Seven), `MobilityModule` (velocidad, doble salto vía `movementY`, skate vía `CurentSettings`, Board Boost), tiendas (`DeliveryShop.GetDeliveryTime/Cost`, pawn `StartNegotiation`/`EvaluateCounter`), especiales (`GetBuyQuantityForPlayerRank`, `bonusPerDayUnseen`, `RunArrivalPhase`), combos en todos los módulos, sinergias nuevas (comps, getaway, momentum). Compila. Falta: prueba en juego.

## v2 terminado (2026-10-16), versión 1.1.0
Probado en el juego (partida Armstrong, restaurada; el diff contra el backup dio 0 cambios):
- UI: el octágono (hover → panel lateral) y el árbol de rama (conectores en codo, puertas de nivel, combos en el panel) se ven y funcionan. También aprender con prerrequisitos y equipar.
- HUD de tarjetas READY / ACTIVE / COOLDOWN, verificado con Sprint Burst.
- Casino (medido con el bridge; **no valía**: en partida real esos métodos están inlineados, ver el registro 2026-10-16 (6)):
  - Slots SmallWin con apuesta de $100: $1,000 → $1,351 (Lucky Charm 3 + The House).
  - Blackjack, victoria con apuesta de $100: $200 → $230.
  - Ride the Bus: x20 → x23.8 (Bus Rider 2).
- Especiales: los grupos son Businessmen, Hippies, Party Bus y Bikers.
  - Scene Regular sube `bonusPerDayUnseen` de 0.6 a 0.96 y vuelve a 0.6 sin el skill.
  - Party Planner se cambió a porcentaje (+15 %/rango) porque los pedidos son de unas 70 unidades.
- Movilidad:
  - El doble salto se dispara (`movementY` 0 → 5.25) y la altura supera la del salto normal: −1.71 frente a −1.87, hasta chocar con el techo.
  - Skate: tope 38.72 → 46.85, salto 49 → 68.6, empuje 11.7 → 15.21; los valores base vuelven al quitar los skills.
  - Board Boost se dispara, pero no se pudo medir la velocidad dentro de la habitación (choca con la pared).
- Sin probar en el juego: deliveries/pawn (UI no instanciada), Peek, Hot Streak, la mayoría de combos y las sinergias nuevas (comps, getaway, momentum).
- Paquete: `dist/HylandSkills-1.1.0.zip` (incluye LICENSE-lucide.txt). `DebugLogging` vuelve a false.
