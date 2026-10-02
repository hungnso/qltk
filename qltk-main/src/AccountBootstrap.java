import java.awt.Frame;
import java.io.*;
import java.lang.reflect.*;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.concurrent.atomic.AtomicReference;
import java.util.jar.*;
import javax.microedition.lcdui.Display;
import javax.microedition.midlet.MIDlet;
import org.microemu.MIDletBridge;
import org.microemu.MIDletContext;
import org.microemu.app.Main;
import org.microemu.app.util.FileRecordStoreManager;
import org.microemu.util.RecordStoreImpl;

/** Starts the existing emulator; no passwords appear in the command line or status. */
public final class AccountBootstrap {
    public static void main(String[] args) {
        if (args.length == 2 && args[0].equals("--verify")) {
            try { verifyContract(new File(args[1])); System.out.println("Game account bridge contract: OK"); }
            catch (Exception e) { System.err.println("Game account bridge contract: incompatible"); System.exit(2); }
            return;
        }
        if (args.length != 8) { System.err.println("Invalid QLTK launch parameters."); System.exit(2); return; }
        File status = new File(args[7]);
        try {
            final File game = new File(args[0]);
            final File home = new File(args[1]);
            final int server = Integer.parseInt(args[2]);
            final int tab = Integer.parseInt(args[3]);
            final int width = Integer.parseInt(args[4]);
            final int height = Integer.parseInt(args[5]);
            final boolean autoLogin = Boolean.parseBoolean(args[6]);
            final String username = System.getenv("QLTK_ACCOUNT_USER");
            final String password = System.getenv("QLTK_ACCOUNT_PASS");
            if (username == null || password == null || server < 0) throw new IOException("Missing account.");
            verifyContract(game);
            prepare(home, game, username, password, server);
            System.setProperty("user.home", home.getAbsolutePath());
            writeStatus(status, "STARTING");
            // The unmodified emulator retains its own MIDlet classloader and device setup.
            Main.main(new String[] { "--tabid", String.valueOf(tab), "--resizableDevice", String.valueOf(width), String.valueOf(height), "--openjar", game.getAbsolutePath() });
            for (Frame frame : Frame.getFrames()) if (frame instanceof Main) frame.setTitle("Tab " + tab + " - " + username);
            if (!autoLogin) { writeStatus(status, "READY_MANUAL"); new GameAccountObserver(status, false).start(); return; }
            loginWhenReady(username, password, server, status);
            new GameAccountObserver(status, true).start();
        } catch (Throwable e) {
            try { writeStatus(status, "ERROR_STARTUP"); } catch (IOException ignored) { }
            System.err.println("QLTK could not start this tab (" + e.getClass().getSimpleName() + ").");
            System.exit(2);
        }
    }
    public static void verifyContract(File game) throws Exception {
        if (!game.isFile()) throw new FileNotFoundException("Game jar missing.");
        try (URLClassLoader loader = new URLClassLoader(new URL[] { game.toURI().toURL() }, AccountBootstrap.class.getClassLoader())) {
            Class<?> login = Class.forName("LoginScr", false, loader);
            login.getDeclaredMethod("mgI");
            try {
                login.getMethod("autoLogin", String.class, String.class);
            } catch (NoSuchMethodException oldLoginApiMissing) {
                Class<?> service = Class.forName("Service", false, loader);
                service.getMethod("gI"); service.getMethod("login", String.class, String.class, String.class);
                Class.forName("SelectServerScr", false, loader).getField("version");
            }
            Class<?> canvas = Class.forName("GameCanvas", false, loader);
            canvas.getDeclaredField("currentScreen"); canvas.getDeclaredField("menu");
            Class<?> midlet = Class.forName("GameMidlet", false, loader);
            for (String name : new String[] { "nameServer", "portList", "language", "serverLoginList", "g", "port", "serverLogin" }) midlet.getDeclaredField(name);
            canvas.getDeclaredField("isLoading");
            Class<?> selector = Class.forName("SelectCharScr", false, loader);
            selector.getDeclaredField("name"); selector.getDeclaredField("indexSelect"); selector.getDeclaredMethod("perform", int.class, Object.class);
            Class<?> character = Class.forName("Char", false, loader);
            character.getDeclaredMethod("getMyChar");
            for (String name : new String[] { "cName", "clevel", "xu", "luong", "xuInBox", "arrItemBox", "arrItemBody" }) character.getDeclaredField(name);
            Class<?> item = Class.forName("Item", false, loader);
            item.getDeclaredField("template"); item.getDeclaredField("upgrade");
            Class<?> itemTemplate = Class.forName("ItemTemplate", false, loader);
            itemTemplate.getDeclaredField("type"); itemTemplate.getDeclaredField("level");
            Class<?> service = Class.forName("Service", false, loader);
            service.getDeclaredMethod("gI"); service.getDeclaredMethod("requestItem", int.class);
        }
    }
    public static void prepare(File home, File game, String username, String password, int server) throws Exception {
        String suite;
        try (JarFile jar = new JarFile(game)) {
            Manifest manifest = jar.getManifest();
            if (manifest == null) throw new IOException("Missing game manifest.");
            suite = manifest.getMainAttributes().getValue("MIDlet-Name");
        }
        if (suite == null || suite.isEmpty() || suite.contains("/") || suite.contains("\\") || suite.contains(":") || suite.equals(".") || suite.equals("..")) throw new IOException("Invalid suite name.");
        File folder = new File(home, ".microemulator/suite-" + suite);
        if (!folder.isDirectory() && !folder.mkdirs()) throw new IOException("Cannot create tab data.");
        store(folder, "acc", username.getBytes(StandardCharsets.UTF_8));
        store(folder, "pass", password.getBytes(StandardCharsets.UTF_8));
        store(folder, "check", new byte[] { 1 });
        store(folder, "indServer", new byte[] { (byte) server });
        store(folder, "AutoAccountRotation_enabled", new byte[] { 0 });
    }
    private static void store(File folder, String key, byte[] bytes) throws Exception {
        String name = "vj" + key;
        FileRecordStoreManager manager = new FileRecordStoreManager() {
            @Override public void saveChanges(RecordStoreImpl store) { }
        };
        RecordStoreImpl record = new RecordStoreImpl(manager, name);
        record.setOpen(true); record.addRecord(bytes, 0, bytes.length);
        File destination = new File(folder, name + ".rs"), temp = new File(folder, name + ".rs.tmp");
        try (DataOutputStream out = new DataOutputStream(new FileOutputStream(temp))) { record.write(out); }
        Files.move(temp.toPath(), destination.toPath(), StandardCopyOption.REPLACE_EXISTING);
    }
    public static void writeStatus(File status, String value) throws IOException {
        File temp = new File(status.getParentFile(), status.getName() + ".tmp");
        Files.write(temp.toPath(), value.getBytes(StandardCharsets.UTF_8));
        Files.move(temp.toPath(), status.toPath(), StandardCopyOption.REPLACE_EXISTING);
    }
    static void loginWhenReady(final String username, final String password, final int server, final File status) throws Exception {
        long deadline = System.currentTimeMillis() + 120000;
        MIDlet midlet = null; ClassLoader loader = null; Object screen = null;
        while (System.currentTimeMillis() < deadline) {
            midlet = MIDletBridge.getCurrentMIDlet();
            if (midlet != null && midlet.getClass().getSimpleName().equals("GameMidlet")) {
                loader = midlet.getClass().getClassLoader();
                screen = Class.forName("GameCanvas", false, loader).getField("currentScreen").get(null);
                if (screen != null && (screen.getClass().getSimpleName().equals("SelectServerScr") || screen.getClass().getSimpleName().equals("LoginScr"))) break;
            }
            Thread.sleep(200);
        }
        if (screen == null || !(screen.getClass().getSimpleName().equals("SelectServerScr") || screen.getClass().getSimpleName().equals("LoginScr"))) {
            writeStatus(status, "ERROR_LOGIN_TIMEOUT"); return;
        }
        final ClassLoader gameLoader = loader;
        final MIDletContext context = MIDletBridge.getMIDletContext(midlet);
        final AtomicReference<Throwable> failure = new AtomicReference<Throwable>();
        final java.util.concurrent.CountDownLatch submitted = new java.util.concurrent.CountDownLatch(1);
        Display.getDisplay(midlet).callSerially(new Runnable() {
            public void run() {
                try {
                    MIDletBridge.setThreadMIDletContext(context);
                    applyLogin(gameLoader, username, password, server);
                    writeStatus(status, "LOGIN_SUBMITTED");
                } catch (Throwable e) {
                    failure.set(e);
                    try { writeStatus(status, "ERROR_LOGIN"); } catch (IOException ignored) { }
                } finally { submitted.countDown(); }
            }
        });
        if (!submitted.await(20, java.util.concurrent.TimeUnit.SECONDS)) writeStatus(status, "ERROR_LOGIN_TIMEOUT");
        else if (failure.get() != null) System.err.println("QLTK login bridge failed (" + failure.get().getClass().getSimpleName() + ").");
    }
    public static void applyLogin(ClassLoader loader, String username, String password, int server) throws Exception {
        Class<?> game = Class.forName("GameMidlet", true, loader);
        String[] hosts = (String[]) game.getField("nameServer").get(null);
        int[] ports = (int[]) game.getField("portList").get(null);
        byte[] languages = (byte[]) game.getField("language").get(null);
        int[] serverIds = (int[]) game.getField("serverLoginList").get(null);
        if (server >= hosts.length || server >= ports.length || server >= languages.length || server >= serverIds.length) throw new IOException("Unsupported server.");
        Class<?> loginType = Class.forName("LoginScr", true, loader);
        Object login = loginType.getMethod("mgI").invoke(null);
        game.getField("g").set(null, hosts[server]);
        game.getField("port").setInt(null, ports[server]);
        game.getField("serverLogin").setByte(null, languages[server]);
        Object menu = Class.forName("GameCanvas", false, loader).getField("menu").get(null);
        if (menu != null) menu.getClass().getField("menuSelectedItem").setInt(menu, serverIds[server]);
        Class.forName("mResources", true, loader).getMethod("a", String.class, int.class).invoke(null, "indServer", serverIds[server]);
        try {
            loginType.getMethod("autoLogin", String.class, String.class).invoke(login, username, password);
        } catch (NoSuchMethodException oldLoginApiMissing) {
            Class<?> serviceType = Class.forName("Service", true, loader);
            Object service = serviceType.getMethod("gI").invoke(null);
            String version = (String) Class.forName("SelectServerScr", true, loader).getField("version").get(null);
            serviceType.getMethod("login", String.class, String.class, String.class).invoke(service, username, password, version);
        }
    }
}
