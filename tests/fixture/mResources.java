public final class mResources {
    public static int selected = -1;
    public static int autoDailyFlags = -1;
    public static byte[] autoBytes;
    public static void a(String key, int value) { if (key.equals("indServer")) selected = value; else if (key.equals("AutoDailyOptions")) autoDailyFlags = value; else throw new IllegalStateException("Unexpected RMS key."); }
    public static void a(String key, byte[] bytes) { if (!key.equals("V7LCSetting")) throw new IllegalStateException("Unexpected RMS key."); autoBytes = bytes; }
    public static byte[] b(String key) { return key.equals("V7LCSetting") ? autoBytes : null; }
    public static int d(String key) { return key.equals("AutoDailyOptions") ? autoDailyFlags : -1; }
}
