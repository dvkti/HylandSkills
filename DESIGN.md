# Hyland Skills: diseño (v2, versión 1.1.0)

**Fuente de verdad:** `HylandSkills/src/Data/SkillDefs.cs`.
- 8 categorías en un octágono: Cultivation, Chemistry, Empire, Gambling, Hustle, Shadows, Mobility, Muscle. Cada una cuesta 24 puntos y hay 50 en total.
- Árboles estilo Cyberpunk: cada skill pide a su padre. Los niveles se abren con 0/3/7/12/17 puntos en la rama y las maestrías en 24.
- 23 combos entre skills relacionadas que cambian la mecánica.
- 8 sinergias en las aristas del octágono.
- Ramas nuevas en v2:
  - **Gambling:** Card Sense, Lucky Charm, Loss Insurance, High Roller, Bus Rider, Laundered Luck, Hot Streak, Peek, Double or Nothing. Maestrías: The House, Lucky Seven, Card Shark.
  - **Mobility:** Fleet Foot, Board Feel, Double Jump, Push Power, Kickflip, Cat Burglar, Air Control, Sprint Burst, Board Boost. Maestrías: Parkour, Street Surfer, Courier.
- Skills nuevas en ramas existentes:
  - Empire: Express Shipping, Pawn Broker.
  - Hustle: Party Planner y Scene Regular, para los crowds especiales.

La tabla de abajo es el diseño v1, conservado como referencia.

---

# Hyland Skills: diseño

Mod de árbol de habilidades para **Schedule I 0.4.7f12 (IL2CPP)**, con MelonLoader 0.7.3 y S1API 3.2.1.
Los textos del juego van en inglés. Este documento está en español.

> **Cambios hechos durante la implementación.** En este juego la salud máxima es una constante compilada que no se puede modificar sin cerrar el juego. Por eso Thick Skin y Juggernaut ahora reducen el daño recibido. La calidad se aplica por cosecha y por lote, en niveles enteros (Trash → Heavenly), porque el juego no guarda la calidad de la planta. Kingmaker y Coca Refiner se adaptaron a lo que el juego permite modificar de forma segura.

## Reglas del sistema

| Regla | Valor |
|---|---|
| Puntos de habilidad | 2 por cada tier subido, desde Street Rat I hasta Kingpin I: **100 en total** (el juego tiene 11 rangos × 5 tiers) |
| Coste de una rama completa | **24 puntos** cada una. Con 8 ramas, la mitad (4 ramas) cuesta 96 y sobran 4 puntos para tocar una quinta |
| Puntos de maestría | **4**: en Bagman I, Shot Caller I, Underlord I y Kingpin I (cada uno llega poco después de poder completar una rama más) |
| Maestría mayor | Exige la rama completa (24/24) y 1 punto de maestría. Se elige **1 de 3** por rama y es exclusiva |
| Filas | Fila 1: libre. Fila 2: 8 puntos en la rama. Fila 3: 16 puntos en la rama |
| Sinergias | El octógono tiene 8 ramas. Dos ramas vecinas con ≥ 8 puntos cada una activan su sinergia gratis |
| Reinicio (respec) | Se paga desde la cuenta del banco: $5,000, $15,000, $30,000, $50,000 y luego $75,000 (tope). No se puede durante una persecución |
| Activas | 4 ranuras equipables. Teclas configurables (por defecto `Z X B N`) |
| Temporizadores | HUD con 9 posiciones posibles (por defecto arriba a la derecha), además de escala y opacidad |
| ⚖ | Nodo con contrapeso: da una ventaja y quita algo. El balance neto siempre es positivo |

Hay pocos nodos de "más velocidad". La mayoría crea un estilo de juego distinto, como cultivo orgánico, química de precisión, marca premium, operador nocturno, tapadera legal o matón.

Orden del hexágono: Cultivation → Chemistry → Empire → Shadows → Hustle → Muscle → (vuelve a Cultivation).

---

## 🌿 CULTIVATION: cultivo artesanal
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Green Thumb | 3×1 | +6 % de cosecha por rango (weed y coca) |
| 1 | Deep Roots | 3×1 | Macetas y camas pierden agua un 12 % más lento por rango |
| 1 | Seed Saver | 2×1 | 12 / 25 % de probabilidad de recuperar la semilla al terminar de cosechar |
| 2 | Green Sense | 3×1 | +6 % por rango de probabilidad de que cada cosecha salga un nivel de calidad más alto |
| 2 | ⚖ Organic Methods | 2 | Sin Speed Grow ni PGR: +10 % de probabilidad de subir un nivel de calidad. Con ellos: −10 % de cosecha |
| 2 | ⚡ Prune | 3 | Activa. Mirando una planta que está creciendo: rinde un 30 % menos, pero todo lo que da sale un nivel de calidad más alto. Recarga: 3 horas de juego |
| 3 | ⚡ Grow Lights Overdrive | 4 | Activa. Durante 4 horas, las plantas de la propiedad crecen +35 %, pero beben el doble de agua. Recarga: 1 día |
| 3 | ⚖ Living Soil | 4 | La tierra no se gasta nunca, pero las plantas crecen un 12 % más lento |
| M | Heirloom Genetics | – | Cada cosecha tiene un 20 % de probabilidad de subir **dos** niveles de calidad |
| M | Mycologist | – | Las colonias de hongos crecen un 25 % más rápido y cada cosecha tiene un 25 % de probabilidad de dar uno extra |
| M | Head Botanist | – | Los Botanists trabajan +30 %, pero piden +20 % de sueldo |

## ⚗️ CHEMISTRY: químico de precisión
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Steady Hands | 3×1 | Chemistry Station y Lab Oven trabajan +6 % más rápido por rango |
| 1 | Precise Measures | 3×1 | +6 % por rango de probabilidad de que meth y coca suban un nivel de calidad |
| 1 | Mixologist | 2×1 | Mixing Station +15 % de velocidad por rango |
| 2 | Ingredient Saver | 3×1 | +7 % por rango de probabilidad de recuperar el ingrediente de mezcla |
| 2 | ⚖ Volatile Chemistry | 2 | Las estaciones químicas van +30 % más rápido, pero un lote de cada 10 baja un nivel de calidad |
| 2 | ⚡ Lab Focus | 3 | Activa. Durante 3 horas, cada lote que empiezas tiene un 35 % de probabilidad de subir un nivel de calidad. Recarga: 1 día |
| 3 | ⚡ Overclock | 4 | Activa. Durante 3 horas, las estaciones de la propiedad van al doble de velocidad. Después, 3 horas al −30 % (agotamiento). Recarga: 1 día |
| 3 | ⚖ Complex Formulas | 4 | Productos con 3 o más efectos se pagan +12 %. Con 1 o 2 efectos, −8 % |
| M | Crystal Purity | – | El 35 % de los lotes de meth sale un nivel de calidad más alto |
| M | Alchemist | – | El 20 % de las mezclas da +20 % de unidades |
| M | Coca Refiner | – | Cada lote del Cauldron devuelve el 25 % de las hojas de coca (5 de 20) |

## 🏢 EMPIRE: tapadera y negocios
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Bookkeeper | 3×1 | Los negocios lavan +10 % de capacidad por rango |
| 1 | Bulk Buyer | 3×1 | Tiendas y proveedores −4 % de precio por rango |
| 1 | Payroll | 2×1 | Sueldos −8 % por rango |
| 2 | ATM Connections | 3×1 | Límite semanal del cajero +$2,500 por rango |
| 2 | ⚖ Overtime Culture | 2 | Empleados +20 % de velocidad, pero sueldos +15 % |
| 2 | ⚡ Rush Delivery | 3 | Activa. El pedido de la tienda llega en 1 hora de juego, con un recargo de $150. Recarga: 1 día |
| 3 | ⚡ Cook the Books | 4 | Activa. Termina al instante todos los lavados en curso, perdiendo un 10 % en comisiones. Recarga: 2 días |
| 3 | ⚖ Legit Fronts | 4 | Cada negocio propio da $120 limpios al día, pero la capacidad de lavado baja −15 % |
| M | Tycoon | – | Lavado +25 % de capacidad y +25 % de velocidad |
| M | Union Boss | – | Empleados +15 % de velocidad y sueldos −10 % |
| M | Offshore Account | – | El límite del cajero se duplica |

## 🌑 SHADOWS: operador nocturno
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Low Profile | 3×1 | La policía tarda +10 % en notarte por rango |
| 1 | Hidden Pockets | 3×1 | +12 % por rango de probabilidad de que un registro no encuentre nada |
| 1 | Light Steps | 2×1 | Pasos un 25 % más silenciosos por rango |
| 2 | Night Owl | 3×1 | Durante el toque de queda, la policía tarda +10 % más en notarte por rango (se suma a Low Profile) |
| 2 | ⚖ Clean Hands | 2 | La policía abandona la búsqueda un 35 % antes, pero las multas se duplican |
| 2 | ⚡ Police Scanner | 3 | Activa. Durante 2 horas, la policía a menos de 60 m aparece marcada en el HUD. Recarga: 1 día |
| 3 | ⚡ Vanish | 4 | Activa. Si ningún oficial te ve, terminas la persecución. Recarga: 10 minutos reales |
| 3 | ⚖ Ghost Protocol | 4 | Nunca te registran de día, pero de noche te notan un 25 % más rápido |
| M | Phantom | – | La búsqueda policial dura −40 % |
| M | Night King | – | Los tratos durante el toque de queda pagan +20 % |
| M | Wheelman | – | Vehículos +10 % de velocidad, y la policía te pierde un 40 % antes si vas conduciendo |

## 💬 HUSTLE: marca y clientela
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Silver Tongue | 3×1 | +8 % por rango de aceptación de contraofertas |
| 1 | Free Taste | 3×1 | +8 % por rango de éxito con las muestras |
| 1 | People Person | 2×1 | +20 % por rango de relación ganada en cada trato |
| 2 | Dealer Network | 3×1 | Los dealers cobran −2 % de comisión por rango |
| 2 | ⚖ Premium Brand | 2 | Los clientes pagan +15 %, pero piden un 20 % menos seguido |
| 2 | ⚡ Hype Text | 3 | Activa. Hasta 3 clientes te piden un trato ahora. Recarga: 1 día |
| 3 | ⚡ Rush Hour | 4 | Activa. Durante 2 horas, tus tratos pagan +20 %. Recarga: 1 día |
| 3 | ⚖ Addiction Economics | 4 | Tu producto es +40 % adictivo, pero los plantones o rechazos dañan el doble la relación |
| M | Street Legend | – | Contraofertas +15 % y propinas en el 15 % de los tratos |
| M | Kingmaker | – | Los dealers cobran otro −5 % de comisión y sus tratos pagan +10 % |
| M | VIP Host | – | Los tratos con Special Customers pagan +25 % |

## 👊 MUSCLE: matón y protección
| Fila | Nodo | Coste | Efecto |
|---|---|---|---|
| 1 | Thick Skin | 3×1 | −5 % de daño recibido por rango |
| 1 | Heavy Hands | 3×1 | +12 % de daño de puño y cuerpo a cuerpo por rango |
| 1 | Steady Aim | 2×1 | −15 % de dispersión de armas por rango |
| 2 | Second Wind | 3×1 | +40 % de regeneración de salud por rango |
| 2 | ⚖ Bruiser Build | 2 | −15 % de daño recibido, pero −6 % de velocidad de movimiento |
| 2 | ⚡ Adrenaline | 3 | Activa. 12 s con −40 % de daño recibido y +20 % de velocidad. Recarga: 3 minutos reales |
| 3 | ⚡ Intimidate | 4 | Activa. Los goons del cártel a menos de 15 m huyen durante 10 s. Recarga: 5 minutos reales |
| 3 | ⚖ Notorious | 4 | El cártel te embosca un 50 % menos, pero la policía te nota un 15 % antes |
| M | Juggernaut | – | −20 % de daño recibido y los golpes ya no te hacen tambalear |
| M | Gunslinger | – | +25 % de daño con armas de fuego |
| M | Protection Racket | – | El cártel no puede robar a tus dealers |

## 🔗 Sinergias (vecinas en el hexágono, ≥ 8 puntos en cada rama)
| Sinergia | Ramas | Efecto |
|---|---|---|
| Farm to Lab | Cultivation + Chemistry | Las estaciones tienen +10 % de probabilidad de subir un nivel de calidad |
| Industrial Scale | Chemistry + Empire | Los Chemists trabajan +20 % más rápido |
| Offshore Accounts | Empire + Shadows | Las multas de la policía bajan −50 % |
| Back Alley | Shadows + Hustle | Los tratos durante el toque de queda pagan +10 % |
| Respect | Hustle + Muscle | +10 % de aceptación de contraofertas |
| Home Turf | Muscle + Cultivation | −30 % de daño recibido dentro de tus propiedades |
