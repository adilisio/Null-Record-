namespace Game.Combat
{
    /// <summary>One way to hit things. Timings are seconds; reach is metres in front of the feet.</summary>
    public sealed class WeaponDef
    {
        /// <summary>Inventory tool id (empty for bare fists, which you always have).</summary>
        public string Id;
        public string Name;
        public int Damage = 1;
        public float Reach = 1.45f;
        public float Startup = 0.06f, Active = 0.12f, Recovery = 0.18f;
        /// <summary>Shock: passes through the Warden's armour and stuns anything it hits for this long.</summary>
        public float Stun;
        /// <summary>Animation trigger for the swing.</summary>
        public string Anim = "Strike";
        /// <summary>Child of the right hand that shows this weapon (none for fists).</summary>
        public string Prop;

        public float Total => Startup + Active + Recovery;
    }

    /// <summary>
    /// Weapons you find lying around. Picking one up equips it; 1/2 (dpad left/right, or the touch weapon button)
    /// switch between those you've found. First-pass numbers, meant to be tuned.
    /// </summary>
    public static class Weapons
    {
        public static readonly WeaponDef Fists = new WeaponDef { Id = "", Name = "Fists" };

        /// <summary>Slow and long: two hits drop an Enforcer. Found in Maintenance.</summary>
        public static readonly WeaponDef Pipe = new WeaponDef
        {
            Id = "weapon_pipe", Name = "Pipe", Damage = 2, Reach = 1.9f, Startup = 0.11f, Active = 0.14f, Recovery = 0.3f,
            Anim = "Swing", Prop = "Weapon_Pipe",
        };

        /// <summary>Quick, light, and shocking: gets through the Warden's armour and stuns. Found in Records.</summary>
        public static readonly WeaponDef ShockBaton = new WeaponDef
        {
            Id = "weapon_baton", Name = "Shock Baton", Damage = 1, Reach = 1.6f, Startup = 0.05f, Active = 0.12f, Recovery = 0.16f,
            Stun = 0.7f, Anim = "Swing", Prop = "Weapon_ShockBaton",
        };

        /// <summary>In switching order.</summary>
        public static readonly WeaponDef[] All = { Fists, Pipe, ShockBaton };

        public static WeaponDef Find(string id)
        {
            foreach (WeaponDef w in All) if (w.Id == id) return w;
            return null;
        }
    }
}
