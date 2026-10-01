import javax.microedition.midlet.MIDlet;
import javax.microedition.lcdui.Display;
import javax.microedition.rms.RecordStore;
public final class GameMidlet extends MIDlet {
    public static String[] nameServer = { "fixture0", "fixture1", "fixture2" };
    public static int[] portList = { 1000, 1001, 1002 };
    public static byte[] language = { 0, 1, 2 };
    public static int[] serverLoginList = { 0, 1, 2 };
    public static String g; public static int port; public static byte serverLogin;
    public void startApp() {
        try {
            if (!read("vjacc").equals("fixture-user") || !read("vjpass").equals(" fixture-pass ")) throw new IllegalStateException("Wrong account seeded before startup.");
        } catch (Exception e) { throw new IllegalStateException("Credential seed failed."); }
        Display.getDisplay(this).setCurrent(new GameCanvas());
    }
    static String read(String name) throws Exception { RecordStore s = RecordStore.openRecordStore(name, false); String value = new String(s.getRecord(1), "UTF-8"); s.closeRecordStore(); return value; }
    public void pauseApp() { }
    public void destroyApp(boolean unconditional) { }
}
