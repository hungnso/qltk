public final class AutoDailyPanel {
    public static boolean weaponOnlyPickup;
    public static int saves;
    public static void toggleWeaponOnlyPickup() {
        setWeaponOnlyPickup(!weaponOnlyPickup, true);
    }
    private static void setWeaponOnlyPickup(boolean enabled, boolean notify) {
        weaponOnlyPickup = enabled;
        if (weaponOnlyPickup) { Char.doa = false; Char.dt = false; }
        saves++;
        mResources.a("AutoDailyOptions", weaponOnlyPickup ? 16 : 0);
    }
}
