using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HylandSkills.Data
{
    /// <summary>Octagon order (clockwise from the top); neighbours share a synergy.</summary>
    public enum Branch { Cultivation, Chemistry, Empire, Gambling, Hustle, Shadows, Mobility, Muscle }

    public enum SkillKind { Passive, Active, Tradeoff, Mastery }

    public enum CooldownClock { RealSeconds, GameMinutes }

    public sealed class SkillDef
    {
        public string Id;
        public Branch Branch;
        /// <summary>Tree level 0-4 (gated by points spent in the branch); 5 = Major Mastery.</summary>
        public int Tier;
        /// <summary>Column 0-4 on the branch grid.</summary>
        public int Col;
        /// <summary>Skills that must be learned (rank 1+) before this one.</summary>
        public string[] Parents = Array.Empty<string>();
        public string Name;
        public SkillKind Kind;
        public int MaxRank = 1;
        public int CostPerRank = 1;
        /// <summary>Main value per rank (percent points, flat amounts…), interpreted by the effect code.</summary>
        public float Value;
        /// <summary>Secondary value (drawback size, duration…), not scaled by rank.</summary>
        public float Value2;
        /// <summary>Description template. {0} = Value × rank, {1} = Value2.</summary>
        public string Desc;
        public string Drawback;
        public string Icon;

        // Active-only data
        public CooldownClock Clock;
        public float Cooldown;
        public float Duration;

        public bool IsActive => Kind == SkillKind.Active;
        public bool IsMastery => Kind == SkillKind.Mastery;
        public int TotalCost => MaxRank * CostPerRank;

        public string Describe(int rank)
        {
            int r = Math.Max(1, rank);
            return string.Format(Desc, Fmt(Value * r), Fmt(Value2));
        }

        public static string Fmt(float v) => Mathf.Approximately(v, Mathf.Round(v)) ? ((int)Mathf.Round(v)).ToString("N0") : v.ToString("0.#");
    }

    public sealed class SynergyDef
    {
        public string Id;
        public Branch A, B;
        public string Name;
        public string Desc;
        public string Icon;
    }

    /// <summary>Two related skills that together change how something works (not just add up).</summary>
    public sealed class ComboDef
    {
        public string Id;
        public string A, B;
        public string Name;
        public string Desc;
    }

    public sealed class BranchInfo
    {
        public Branch Branch;
        public string Name;
        public string Tagline;
        public string Blurb;
        public Color Color;
        public string Icon;
    }

    public static class SkillDb
    {
        /// <summary>2 per tier-up from Street Rat I to Kingpin I: half of the 8 trees (4 × 24) plus 4 spare.</summary>
        public const int TotalSkillPoints = 100;
        public const int PointsPerBranch = 24;
        public const int SynergyRequirement = 8;
        /// <summary>Points that must be spent in a branch to open each tree level (index = tier).</summary>
        public static readonly int[] TierGate = { 0, 3, 7, 12, 17, 24 };

        public static readonly List<SkillDef> All = new List<SkillDef>();
        public static readonly Dictionary<string, SkillDef> ById = new Dictionary<string, SkillDef>();
        public static readonly List<SynergyDef> Synergies = new List<SynergyDef>();
        public static readonly List<ComboDef> Combos = new List<ComboDef>();

        public static readonly BranchInfo[] Branches =
        {
            new BranchInfo { Branch = Branch.Cultivation, Name = "Cultivation", Color = Hex("6FBF4A"), Icon = "branch_cultivation",
                Tagline = "Grow it right.", Blurb = "Yield, water, seeds and harvest quality. Go organic, or never buy soil again." },
            new BranchInfo { Branch = Branch.Chemistry, Name = "Chemistry", Color = Hex("3FA7D6"), Icon = "branch_chemistry",
                Tagline = "Precision is profit.", Blurb = "Faster stations, better batches, fewer wasted ingredients. Push the lab past its limits." },
            new BranchInfo { Branch = Branch.Empire, Name = "Empire", Color = Hex("E0B33A"), Icon = "branch_empire",
                Tagline = "Clean money, clean hands.", Blurb = "Laundering, staff, shops, deliveries and the pawn shop. Run it like a business." },
            new BranchInfo { Branch = Branch.Gambling, Name = "Gambling", Color = Hex("D65DB1"), Icon = "branch_gambling",
                Tagline = "The house always wins. Be the house.", Blurb = "Blackjack, slots and Ride the Bus. Bigger tables, softer losses, cleaner winnings." },
            new BranchInfo { Branch = Branch.Hustle, Name = "Hustle", Color = Hex("F08A3C"), Icon = "branch_hustle",
                Tagline = "Every deal is personal.", Blurb = "Haggling, samples, dealers and the special crowds that roll into town." },
            new BranchInfo { Branch = Branch.Shadows, Name = "Shadows", Color = Hex("8C6FD1"), Icon = "branch_shadows",
                Tagline = "Never seen, never caught.", Blurb = "Stay off the radar: slower police, clean searches, shorter pursuits." },
            new BranchInfo { Branch = Branch.Mobility, Name = "Mobility", Color = Hex("4FC9B0"), Icon = "branch_mobility",
                Tagline = "Always one step ahead.", Blurb = "Run faster, jump twice and own the streets on your skateboard." },
            new BranchInfo { Branch = Branch.Muscle, Name = "Muscle", Color = Hex("D9473F"), Icon = "branch_muscle",
                Tagline = "Respect is earned.", Blurb = "Take hits, hit back harder and keep the cartel at a distance." },
        };

        public static BranchInfo Info(Branch b) => Branches[(int)b];
        public static SkillDef Get(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;
        public static IEnumerable<ComboDef> CombosOf(string id) => Combos.Where(c => c.A == id || c.B == id);

        static SkillDb()
        {
            // =========================================================== CULTIVATION
            var b = Branch.Cultivation;
            P(b, 0, 1, "green_thumb", "Green Thumb", 3, 6, "Plants yield {0}% more product.", null, "sprout");
            P(b, 0, 3, "deep_roots", "Deep Roots", 3, 12, "Pots and beds lose water {0}% slower.", null, "droplets");
            P(b, 1, 0, "seed_saver", "Seed Saver", 2, 12.5f, "{0}% chance to get the seed back after a full harvest.", P_("green_thumb"), "bean");
            P(b, 1, 2, "green_sense", "Green Sense", 3, 6, "Each harvest has a {0}% chance to come out one quality tier higher.", P_("green_thumb"), "sparkles");
            T(b, 2, 1, "organic_methods", "Organic Methods", 2, 10, 10, "Plants grown without Speed Grow or PGR get a +{0}% chance to come out one tier higher.",
                "Plants grown with them yield {1}% less.", P_("green_sense"), "leaf");
            A(b, 2, 3, "prune", "Prune", 3, 30, 1, CooldownClock.GameMinutes, 180, 0,
                "Look at a growing plant: it yields {0}% less, but everything it gives comes out {1} quality tier higher.", P_("green_sense"), "scissors");
            T(b, 3, 4, "living_soil", "Living Soil", 4, 0, 12, "Soil never wears out.", "Plants grow {1}% slower.", P_("deep_roots"), "shovel");
            A(b, 4, 2, "grow_overdrive", "Grow Lights Overdrive", 4, 35, 2, CooldownClock.GameMinutes, 1440, 240,
                "For 4 hours, plants on this property grow {0}% faster but drink water {1}x as fast.", P_("prune"), "sun");
            M(b, 1, "heirloom_genetics", "Heirloom Genetics", 20, "Each harvest has a {0}% chance to come out two quality tiers higher.", "dna");
            M(b, 2, "mycologist", "Mycologist", 25, "Mushroom colonies grow {0}% faster and each shroom harvest has a {0}% chance to give one extra.", "umbrella");
            M(b, 3, "head_botanist", "Head Botanist", 30, "Botanists work {0}% faster.", "user-cog", 20, "Botanists ask for {1}% higher wages.");
            C("hand_tended", "prune", "organic_methods", "Hand-Tended", "Pruning an organic plant only costs half the yield.");
            C("hydro_rig", "grow_overdrive", "deep_roots", "Hydro Rig", "Overdrive no longer makes plants drink more water.");
            C("closed_loop", "seed_saver", "living_soil", "Closed Loop", "Seed Saver's chance is doubled.");

            // =========================================================== CHEMISTRY
            b = Branch.Chemistry;
            P(b, 0, 1, "steady_hands", "Steady Hands", 3, 6, "Chemistry stations and lab ovens work {0}% faster.", null, "hand");
            P(b, 0, 3, "mixologist", "Mixologist", 2, 15, "Mixing stations work {0}% faster.", null, "beaker");
            P(b, 1, 0, "precise_measures", "Precise Measures", 3, 6, "{0}% chance for meth and cocaine batches to come out one tier higher.", P_("steady_hands"), "pipette");
            P(b, 1, 4, "ingredient_saver", "Ingredient Saver", 3, 7, "{0}% chance to get the mixer ingredients back when a mix finishes.", P_("mixologist"), "undo-2");
            A(b, 2, 0, "lab_focus", "Lab Focus", 3, 35, 0, CooldownClock.GameMinutes, 1440, 180,
                "For 3 hours, every batch you finish has a {0}% chance to come out one tier higher.", P_("precise_measures"), "scan-eye");
            T(b, 2, 2, "volatile_chemistry", "Volatile Chemistry", 2, 30, 10, "Chemistry stations, lab ovens and cauldrons work {0}% faster.",
                "{1}% of batches drop one quality tier.", P_("steady_hands"), "flame");
            T(b, 3, 4, "complex_formulas", "Complex Formulas", 4, 12, 8, "Products with 3+ effects sell for {0}% more.",
                "Products with 1-2 effects sell for {1}% less.", P_("ingredient_saver"), "atom");
            A(b, 4, 2, "overclock", "Overclock", 4, 100, 30, CooldownClock.GameMinutes, 1440, 180,
                "For 3 hours, stations on this property work {0}% faster. Then they burn out: {1}% slower for 3 hours.", P_("volatile_chemistry"), "gauge");
            M(b, 1, "crystal_purity", "Crystal Purity", 35, "{0}% of meth batches come out one quality tier higher.", "gem");
            M(b, 2, "alchemist", "Alchemist", 35, "{0}% of mixes produce 25% extra units.", "wand-sparkles");
            M(b, 3, "coca_refiner", "Coca Refiner", 25, "Every cauldron batch gives back {0}% of its coca leaves.", "filter");
            C("controlled_burn", "volatile_chemistry", "precise_measures", "Controlled Burn", "A batch that would drop a tier gets a Precise Measures roll to go up instead.");
            C("mad_scientist", "overclock", "lab_focus", "Mad Scientist", "While Overclock and Lab Focus are both running, every batch comes out one tier higher.");
            C("signature_recipe", "lab_focus", "complex_formulas", "Signature Recipe", "Mixes that make a product with 3+ effects always give the ingredients back.");

            // =========================================================== EMPIRE
            b = Branch.Empire;
            P(b, 0, 1, "bookkeeper", "Bookkeeper", 3, 10, "Businesses can launder {0}% more cash.", null, "book-open-text");
            P(b, 0, 3, "bulk_buyer", "Bulk Buyer", 3, 4, "Shop prices are {0}% lower.", null, "package");
            P(b, 1, 0, "payroll", "Payroll", 2, 8, "Employee wages are {0}% lower.", P_("bookkeeper"), "wallet");
            P(b, 1, 2, "atm_connections", "ATM Connections", 2, 2500, "Weekly ATM deposit limit +${0}.", P_("bookkeeper"), "landmark");
            P(b, 1, 4, "express_shipping", "Express Shipping", 2, 20, "Shop deliveries arrive {0}% sooner.", P_("bulk_buyer"), "truck");
            T(b, 2, 0, "overtime_culture", "Overtime Culture", 2, 20, 15, "Employees work {0}% faster.", "Employee wages are {1}% higher.", P_("payroll"), "alarm-clock");
            P(b, 2, 3, "pawn_broker", "Pawn Broker", 2, 10, "The pawn shop opens {0}% higher and haggles more calmly.", P_("bulk_buyer"), "scale");
            T(b, 3, 1, "legit_fronts", "Legit Fronts", 2, 120, 15, "Each business you own earns ${0} clean money per day.",
                "Laundering capacity is {1}% lower.", P_("atm_connections"), "store");
            A(b, 3, 4, "rush_delivery", "Rush Delivery", 3, 60, 150, CooldownClock.GameMinutes, 1440, 0,
                "Your pending shop delivery arrives within {0} minutes. Costs a ${1} fee.", P_("express_shipping"), "package-check");
            A(b, 4, 2, "cook_the_books", "Cook the Books", 3, 10, 0, CooldownClock.GameMinutes, 2880, 0,
                "Instantly finish all laundering operations, losing {0}% as fees.", P_("legit_fronts"), "file-pen");
            M(b, 1, "tycoon", "Tycoon", 25, "Laundering capacity +{0}% and operations finish {0}% faster.", "crown");
            M(b, 2, "union_boss", "Union Boss", 15, "Employees work {0}% faster and wages are 10% lower.", "users");
            M(b, 3, "offshore_account", "Offshore Account", 100, "Weekly ATM deposit limit is doubled.", "tree-palm");
            C("paper_trail", "payroll", "cook_the_books", "Paper Trail", "Cook the Books charges no fees.");
            C("wholesale_account", "pawn_broker", "express_shipping", "Wholesale Account", "Shop deliveries are free.");
            C("company_culture", "legit_fronts", "overtime_culture", "Company Culture", "Overtime Culture no longer raises wages.");

            // =========================================================== GAMBLING
            b = Branch.Gambling;
            P(b, 0, 1, "card_sense", "Card Sense", 3, 1.5f, "Blackjack wins pay {0}% more.", null, "spade");
            P(b, 0, 3, "lucky_charm", "Lucky Charm", 3, 3, "Slot machine wins pay {0}% more.", null, "clover");
            P(b, 1, 0, "loss_insurance", "Loss Insurance", 3, 10, "Get {0}% of a lost Blackjack or Ride the Bus bet back. Casino refunds stop at $1,000 a day.", P_("card_sense"), "life-buoy");
            P(b, 1, 2, "high_roller", "High Roller", 2, 50, "Table betting limits are {0}% higher.", P_("card_sense"), "coins");
            P(b, 1, 4, "bus_rider", "Bus Rider", 2, 3, "Ride the Bus multipliers are {0}% higher.", P_("lucky_charm"), "bus");
            P(b, 2, 1, "laundered_luck", "Laundered Luck", 2, 20, "{0}% of your casino winnings go straight to your bank as clean money.", P_("high_roller"), "piggy-bank");
            A(b, 2, 3, "hot_streak", "Hot Streak", 3, 15, 0, CooldownClock.GameMinutes, 1440, 120,
                "For 2 hours, every casino win pays {0}% more.", P_("lucky_charm"), "flame-kindling");
            A(b, 3, 0, "peek", "Peek", 3, 0, 0, CooldownClock.GameMinutes, 1440, 0,
                "At the blackjack table: see the dealer's face-down card.", P_("loss_insurance"), "eye");
            T(b, 4, 2, "double_or_nothing", "Double or Nothing", 3, 20, 20, "Blackjack wins pay {0}% more.",
                "Losing a blackjack hand costs an extra {1}% of the bet.", P_("laundered_luck"), "repeat-2");
            M(b, 1, "the_house", "The House", 3, "Every casino payout is {0}% bigger.", "castle");
            M(b, 2, "lucky_seven", "Lucky Seven", 4, "{0}% of losing slot spins turn into a small win.", "cherry");
            M(b, 3, "card_shark", "Card Shark", 20, "When you bust at blackjack, {0}% chance to get your whole bet back (daily refund limit applies).", "club");
            C("write_off", "loss_insurance", "laundered_luck", "Write-Off", "Loss Insurance refunds are doubled and paid to your bank.");
            C("jackpot_fever", "hot_streak", "bus_rider", "Jackpot Fever", "During Hot Streak, 5% of losing slot spins turn into a small win.");
            C("read_the_table", "peek", "high_roller", "Read the Table", "Peek recharges twice as fast.");

            // =========================================================== HUSTLE
            b = Branch.Hustle;
            P(b, 0, 1, "silver_tongue", "Silver Tongue", 3, 8, "Customers accept counteroffers up to {0}% above what they would normally pay.", null, "message-circle");
            P(b, 0, 3, "free_taste", "Free Taste", 3, 8, "Samples are {0}% more likely to win a customer.", null, "gift");
            P(b, 1, 0, "dealer_network", "Dealer Network", 2, 3, "Dealers take a {0}% smaller cut.", P_("silver_tongue"), "network");
            P(b, 1, 2, "party_planner", "Party Planner", 2, 15, "Special crowds (Hippies, Bikers, Businessmen, Party Bus) buy {0}% more per deal.", P_("silver_tongue"), "party-popper");
            P(b, 1, 4, "people_person", "People Person", 2, 20, "Deals build {0}% more relationship.", P_("free_taste"), "heart-handshake");
            T(b, 2, 1, "premium_brand", "Premium Brand", 2, 15, 20, "Customers pay {0}% more per deal.", "Customers order {1}% less often.", P_("party_planner"), "tag");
            A(b, 2, 4, "hype_text", "Hype Text", 3, 3, 0, CooldownClock.GameMinutes, 1440, 0,
                "Text your contacts: up to {0} customers ask for a deal right now.", P_("people_person"), "smartphone");
            T(b, 3, 0, "addiction_economics", "Addiction Economics", 2, 40, 100, "Your products are {0}% more addictive.",
                "Rejected or missed deals hurt relationships {1}% more.", P_("dealer_network"), "trending-up");
            P(b, 3, 2, "scene_regular", "Scene Regular", 2, 30, "Special customer crowds come back to town {0}% more often.", P_("party_planner"), "tent");
            A(b, 4, 3, "rush_hour", "Rush Hour", 3, 20, 0, CooldownClock.GameMinutes, 1440, 120,
                "For 2 hours, deals you complete pay {0}% more.", P_("hype_text"), "timer");
            M(b, 1, "street_legend", "Street Legend", 10, "Customers accept counteroffers up to {0}% higher, and 15% of deals include a tip.", "star");
            M(b, 2, "kingmaker", "Kingmaker", 5, "Dealers take another {0}% smaller cut and their deals pay 10% more.", "medal");
            M(b, 3, "vip_host", "VIP Host", 25, "Special customer deals pay {0}% more.", "ticket");
            C("limited_drop", "rush_hour", "premium_brand", "Limited Drop", "During Rush Hour, customers accept counteroffers up to 15% higher.");
            C("viral", "hype_text", "party_planner", "Viral", "Customers who answer Hype Text like you a little more.");
            C("afterparty", "people_person", "scene_regular", "Afterparty", "Special customer crowds stay in town one extra day.");

            // =========================================================== SHADOWS
            b = Branch.Shadows;
            P(b, 0, 1, "low_profile", "Low Profile", 3, 10, "Police take {0}% longer to notice you.", null, "eye-off");
            P(b, 0, 3, "light_steps", "Light Steps", 2, 25, "Your footsteps are {0}% quieter.", null, "footprints");
            P(b, 1, 0, "hidden_pockets", "Hidden Pockets", 3, 12, "{0}% chance a police body search finds nothing.", P_("low_profile"), "shirt");
            P(b, 1, 2, "night_owl", "Night Owl", 3, 10, "During curfew, police take an extra {0}% longer to notice you.", P_("low_profile"), "moon");
            T(b, 2, 0, "clean_hands", "Clean Hands", 2, 35, 100, "Police give up searching for you {0}% sooner.", "Fines when arrested are {1}% higher.", P_("hidden_pockets"), "eraser");
            A(b, 2, 2, "police_scanner", "Police Scanner", 3, 60, 0, CooldownClock.GameMinutes, 1440, 120,
                "For 2 hours, police within {0}m are marked on your screen.", P_("night_owl"), "radio");
            A(b, 3, 4, "vanish", "Vanish", 4, 0, 0, CooldownClock.RealSeconds, 600, 0,
                "If no officer can see you, the pursuit ends instantly.", P_("light_steps"), "ghost");
            T(b, 4, 1, "ghost_protocol", "Ghost Protocol", 4, 0, 25, "Police never body-search you during the day.",
                "During curfew, police notice you {1}% faster.", P_("clean_hands"), "sun-dim");
            M(b, 1, "phantom", "Phantom", 40, "Police search for you {0}% shorter.", "venetian-mask");
            M(b, 2, "night_king", "Night King", 20, "Deals completed during curfew pay {0}% more.", "moon-star");
            M(b, 3, "wheelman", "Wheelman", 10, "Vehicles are {0}% faster and police lose you 40% sooner while driving.", "car");
            C("night_vision", "police_scanner", "light_steps", "Night Vision", "Used during curfew, Police Scanner lasts until the curfew ends.");
            C("smoke_and_mirrors", "vanish", "clean_hands", "Smoke & Mirrors", "Vanish also gives you 10 seconds of 25% extra speed.");
            C("nothing_to_see", "hidden_pockets", "night_owl", "Nothing to See", "An officer whose search came up clean ignores you for an hour.");

            // =========================================================== MOBILITY
            b = Branch.Mobility;
            P(b, 0, 1, "fleet_foot", "Fleet Foot", 3, 4, "You move {0}% faster on foot.", null, "wind");
            P(b, 0, 3, "board_feel", "Board Feel", 3, 7, "Your skateboard's top speed is {0}% higher.", null, "circle-gauge");
            P(b, 1, 0, "double_jump", "Double Jump", 1, 0, "Press jump in mid-air to jump again.", P_("fleet_foot"), "chevrons-up", cost: 3);
            P(b, 1, 2, "push_power", "Push Power", 2, 15, "Skateboard pushes are {0}% stronger.", P_("board_feel"), "fast-forward");
            P(b, 1, 4, "kickflip", "Kickflip", 2, 5, "Skateboard jumps are {0}% higher.", P_("board_feel"), "move-up");
            P(b, 2, 0, "cat_burglar", "Cat Burglar", 1, 30, "You move {0}% faster while crouched.", P_("double_jump"), "cat", cost: 2);
            T(b, 2, 4, "air_control", "Air Control", 2, 40, 10, "Your board stays level in the air: leaning forward or back tilts it {0}% less.", "Your board turns {1}% slower on the ground.", P_("kickflip"), "feather");
            A(b, 3, 1, "sprint_burst", "Sprint Burst", 3, 40, 0, CooldownClock.RealSeconds, 90, 8,
                "For 8 seconds, move {0}% faster.", P_("fleet_foot"), "rabbit");
            A(b, 4, 3, "board_boost", "Board Boost", 4, 20, 0, CooldownClock.RealSeconds, 30, 0,
                "While skating: an instant burst of {0} km/h.", P_("push_power"), "rocket");
            M(b, 1, "parkour", "Parkour", 0, "Double Jump becomes a triple jump, and every mid-air jump is 20% higher.", "mountain");
            M(b, 2, "street_surfer", "Street Surfer", 15, "Your skateboard is {0}% faster and pushes recharge twice as fast.", "waves");
            M(b, 3, "courier", "Courier", 10, "You move {0}% faster while carrying product.", "backpack");
            C("free_runner", "double_jump", "sprint_burst", "Free Runner", "Jumping in mid-air during Sprint Burst is 50% higher and adds 1 second to it.");
            C("big_air", "air_control", "push_power", "Big Air", "Skateboard jumps carry you a bit further forward.");

            // =========================================================== MUSCLE
            b = Branch.Muscle;
            P(b, 0, 1, "thick_skin", "Thick Skin", 3, 5, "Take {0}% less damage from every source.", null, "shield");
            P(b, 0, 3, "heavy_hands", "Heavy Hands", 3, 12, "Punches and melee weapons deal {0}% more damage.", null, "hammer");
            P(b, 1, 0, "second_wind", "Second Wind", 3, 40, "Health regenerates {0}% faster.", P_("thick_skin"), "heart-pulse");
            P(b, 1, 4, "steady_aim", "Steady Aim", 2, 15, "Firearm spread is {0}% lower.", P_("heavy_hands"), "crosshair");
            T(b, 2, 1, "bruiser_build", "Bruiser Build", 2, 15, 6, "Take {0}% less damage.", "Move {1}% slower.", P_("thick_skin"), "shield-half");
            A(b, 2, 3, "adrenaline", "Adrenaline", 3, 40, 20, CooldownClock.RealSeconds, 180, 12,
                "For 12 seconds, take {0}% less damage and move {1}% faster.", P_("heavy_hands"), "zap");
            T(b, 3, 0, "notorious", "Notorious", 4, 50, 15, "The cartel ambushes you {0}% less often.", "Police notice you {1}% faster.", P_("bruiser_build"), "siren");
            A(b, 4, 3, "intimidate", "Intimidate", 4, 15, 10, CooldownClock.RealSeconds, 300, 10,
                "Cartel goons within {0}m flee from you for {1} seconds.", P_("adrenaline"), "skull");
            M(b, 1, "juggernaut", "Juggernaut", 20, "Take {0}% less damage and hits no longer make you flinch.", "shield-plus");
            M(b, 2, "gunslinger", "Gunslinger", 25, "Firearms deal {0}% more damage.", "target");
            M(b, 3, "protection_racket", "Protection Racket", 0, "The cartel can no longer rob your dealers.", "shield-check");
            C("rush", "adrenaline", "second_wind", "Rush", "Adrenaline also heals 30% of your missing health.");
            C("reputation", "intimidate", "notorious", "Reputation", "Goons that flee from Intimidate drop some cash.");

            // =========================================================== SYNERGIES (octagon edges)
            S("farm_to_lab", Branch.Cultivation, Branch.Chemistry, "Farm to Lab", "Stations have +10% chance to output one quality tier higher.");
            S("industrial_scale", Branch.Chemistry, Branch.Empire, "Industrial Scale", "Chemists work 20% faster.");
            S("comps", Branch.Empire, Branch.Gambling, "Comps", "The casino gives 3% of your losses back to your bank (daily refund limit applies).");
            S("respect", Branch.Gambling, Branch.Hustle, "Poker Face", "Customers accept counteroffers up to 10% higher.");
            S("back_alley", Branch.Hustle, Branch.Shadows, "Back Alley", "Deals completed during curfew pay 10% more.");
            S("getaway", Branch.Shadows, Branch.Mobility, "Getaway", "Police search for you 20% shorter.");
            S("momentum", Branch.Mobility, Branch.Muscle, "Momentum", "Punches thrown while sprinting deal 25% more damage.");
            S("home_turf", Branch.Muscle, Branch.Cultivation, "Home Turf", "Take 30% less damage inside your properties.");
        }

        static string[] P_(params string[] ids) => ids;

        static void Add(SkillDef d, string icon)
        {
            d.Icon = icon ?? d.Id;
            All.Add(d);
            ById[d.Id] = d;
        }

        static void P(Branch b, int tier, int col, string id, string name, int maxRank, float value, string desc, string[] parents, string icon, int cost = 1)
            => Add(new SkillDef { Branch = b, Tier = tier, Col = col, Id = id, Name = name, Kind = SkillKind.Passive, MaxRank = maxRank, CostPerRank = cost, Value = value, Desc = desc, Parents = parents ?? Array.Empty<string>() }, icon);

        static void T(Branch b, int tier, int col, string id, string name, int cost, float value, float value2, string desc, string drawback, string[] parents, string icon)
            => Add(new SkillDef { Branch = b, Tier = tier, Col = col, Id = id, Name = name, Kind = SkillKind.Tradeoff, MaxRank = 1, CostPerRank = cost, Value = value, Value2 = value2, Desc = desc, Drawback = drawback, Parents = parents ?? Array.Empty<string>() }, icon);

        static void A(Branch b, int tier, int col, string id, string name, int cost, float value, float value2, CooldownClock clock, float cooldown, float duration, string desc, string[] parents, string icon)
            => Add(new SkillDef { Branch = b, Tier = tier, Col = col, Id = id, Name = name, Kind = SkillKind.Active, MaxRank = 1, CostPerRank = cost, Value = value, Value2 = value2, Clock = clock, Cooldown = cooldown, Duration = duration, Desc = desc, Parents = parents ?? Array.Empty<string>() }, icon);

        static void M(Branch b, int col, string id, string name, float value, string desc, string icon, float value2 = 0, string drawback = null)
            => Add(new SkillDef { Branch = b, Tier = 5, Col = col, Id = id, Name = name, Kind = SkillKind.Mastery, MaxRank = 1, CostPerRank = 0, Value = value, Value2 = value2, Desc = desc, Drawback = drawback }, icon);

        static void S(string id, Branch a, Branch b, string name, string desc)
            => Synergies.Add(new SynergyDef { Id = id, A = a, B = b, Name = name, Desc = desc, Icon = "diamond" });

        static void C(string id, string a, string b, string name, string desc)
            => Combos.Add(new ComboDef { Id = id, A = a, B = b, Name = name, Desc = desc });

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }
    }
}
