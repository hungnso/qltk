public final class mResources {
    public static int selected = -1;
    public static void a(String key, int value) { if (!key.equals("indServer")) throw new IllegalStateException("Unexpected RMS key."); selected = value; }
}
