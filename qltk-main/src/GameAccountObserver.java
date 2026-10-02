import java.io.*;
import java.lang.reflect.*;
import java.nio.file.*;
import java.util.concurrent.atomic.AtomicBoolean;
import javax.xml.stream.*;
import javax.microedition.lcdui.Display;
import javax.microedition.midlet.MIDlet;
import org.microemu.MIDletBridge;
import org.microemu.MIDletContext;

/** All game interaction runs on MicroEmulator's event thread. */
public final class GameAccountObserver {
    private final File status, snapshot;
    private final boolean autoSelect;
    private Object lastSelector, lastCharacter;
    private boolean selectionSent;
    private int chestRequests;
    private long lastChestRequest;
    private String weaponState = "UNKNOWN";
    private int weaponLevel, weaponUpgrade;
    private final ItemStatistics statistics;
    public GameAccountObserver(File status, boolean autoSelect) {
        this(status, autoSelect, System.getenv("QLTK_STATISTICS_FILE") == null ? null : new File(System.getenv("QLTK_STATISTICS_FILE")));
    }
    public GameAccountObserver(File status, boolean autoSelect, File config) {
        this.status = status; this.snapshot = new File(status.getParentFile(), "character.xml"); this.autoSelect = autoSelect;
        statistics = new ItemStatistics(config);
    }
    public void start() {
        final AtomicBoolean pending = new AtomicBoolean();
        Thread worker = new Thread(new Runnable() {
            public void run() {
                while (!Thread.currentThread().isInterrupted()) {
                    try {
                        final MIDlet midlet = MIDletBridge.getCurrentMIDlet();
                        if (midlet != null && midlet.getClass().getSimpleName().equals("GameMidlet") && pending.compareAndSet(false, true)) {
                            final ClassLoader loader = midlet.getClass().getClassLoader();
                            final MIDletContext context = MIDletBridge.getMIDletContext(midlet);
                            try {
                                Display.getDisplay(midlet).callSerially(new Runnable() {
                                    public void run() {
                                        try { MIDletBridge.setThreadMIDletContext(context); tick(loader); }
                                        catch (Throwable e) { reportError(); }
                                        finally { pending.set(false); }
                                    }
                                });
                            } catch (Throwable e) { pending.set(false); reportError(); }
                        }
                        Thread.sleep(2000);
                    } catch (InterruptedException e) { Thread.currentThread().interrupt(); }
                    catch (Throwable e) { reportError(); try { Thread.sleep(2000); } catch (InterruptedException interrupted) { Thread.currentThread().interrupt(); } }
                }
            }
        }, "QLTK-character-observer");
        worker.setDaemon(true); worker.start();
    }
    public void tick(ClassLoader loader) throws Exception {
        Class<?> canvas = Class.forName("GameCanvas", false, loader);
        Object screen = canvas.getField("currentScreen").get(null);
        if (screen == null) { writeSnapshot("WAITING", null, 0, 0, 0, false, 0); return; }
        boolean loading = canvas.getField("isLoading").getBoolean(null);
        String screenType = screen.getClass().getSimpleName();
        if (screenType.equals("SelectCharScr")) {
            if (screen != lastSelector) { lastSelector = screen; selectionSent = false; }
            String[] names = (String[]) screen.getClass().getField("name").get(screen);
            if (loading || names == null || names.length == 0) { writeSnapshot("WAITING", null, 0, 0, 0, false, 0); return; }
            if (autoSelect && (names[0] == null || names[0].trim().isEmpty())) {
                AccountBootstrap.writeStatus(status, "CHARACTER_SLOT_EMPTY"); writeSnapshot("EMPTY_FIRST", null, 0, 0, 0, false, 0); return;
            }
            if (autoSelect && !selectionSent) {
                screen.getClass().getField("indexSelect").setInt(screen, 0);
                // Validate the exact first slot before invoking the game's select action.
                screen.getClass().getMethod("perform", int.class, Object.class).invoke(screen, 1000, null);
                selectionSent = true; AccountBootstrap.writeStatus(status, "SELECTING_CHARACTER");
            }
            writeSnapshot("WAITING", null, 0, 0, 0, false, 0); return;
        }
        lastSelector = null;
        if (!screenType.equals("GameScr") || loading) { writeSnapshot("WAITING", null, 0, 0, 0, false, 0); return; }
        Class<?> type = Class.forName("Char", true, loader);
        Object character = type.getMethod("getMyChar").invoke(null);
        String name = character == null ? null : (String) type.getField("cName").get(character);
        if (name == null || name.isEmpty()) { writeSnapshot("WAITING", null, 0, 0, 0, false, 0); return; }
        if (character != lastCharacter) { lastCharacter = character; chestRequests = 0; lastChestRequest = 0; }
        if (type.getField("arrItemBox").get(character) == null && chestRequests < 3 && System.currentTimeMillis() - lastChestRequest >= 30000) {
            Class<?> service = Class.forName("Service", true, loader);
            Object instance = service.getMethod("gI").invoke(null);
            service.getMethod("requestItem", int.class).invoke(instance, 4);
            lastChestRequest = System.currentTimeMillis(); chestRequests++;
        }
        boolean boxKnown = type.getField("arrItemBox").get(character) != null;
        readWeapon(type, character);
        statistics.read(loader, type, character);
        writeSnapshot("READY", name, type.getField("clevel").getInt(character), type.getField("xu").getInt(character),
            type.getField("luong").getInt(character), boxKnown, boxKnown ? type.getField("xuInBox").getInt(character) : 0);
        AccountBootstrap.writeStatus(status, "IN_GAME");
    }
    private void readWeapon(Class<?> type, Object character) throws Exception {
        weaponState = "UNKNOWN";
        Object[] equipment = (Object[])type.getField("arrItemBody").get(character);
        if (equipment == null || equipment.length <= 1) return;
        Object weapon = equipment[1];
        if (weapon == null) { weaponState = "NONE"; return; }
        Object template = weapon.getClass().getField("template").get(weapon);
        if (template == null || template.getClass().getField("type").getByte(template) != 1) return;
        weaponLevel = template.getClass().getField("level").getByte(template) & 0xff;
        weaponUpgrade = weapon.getClass().getField("upgrade").getInt(weapon);
        if (weaponUpgrade < 0) return;
        weaponState = "EQUIPPED";
    }
    private void reportError() {
        try { AccountBootstrap.writeStatus(status, "ERROR_CHARACTER"); writeSnapshot("ERROR", null, 0, 0, 0, false, 0); } catch (Exception ignored) { }
    }
    private void writeSnapshot(String state, String name, int level, int xu, int luong, boolean boxKnown, int boxXu) throws Exception {
        File temp = new File(snapshot.getParentFile(), snapshot.getName() + ".tmp");
        try (OutputStream out = new FileOutputStream(temp)) {
            XMLStreamWriter xml = XMLOutputFactory.newFactory().createXMLStreamWriter(out, "UTF-8");
            xml.writeStartDocument("UTF-8", "1.0"); xml.writeStartElement("Character"); xml.writeAttribute("state", state);
            if (name != null) {
                xml.writeAttribute("name", name); xml.writeAttribute("level", String.valueOf(level));
                xml.writeAttribute("xu", String.valueOf(xu)); xml.writeAttribute("luong", String.valueOf(luong));
                xml.writeAttribute("boxKnown", String.valueOf(boxKnown));
                if (boxKnown) xml.writeAttribute("boxXu", String.valueOf(boxXu));
                xml.writeAttribute("weaponState", weaponState);
                if (weaponState.equals("EQUIPPED")) {
                    xml.writeAttribute("weaponLevel", String.valueOf(weaponLevel));
                    xml.writeAttribute("weaponUpgrade", String.valueOf(weaponUpgrade));
                }
                statistics.write(xml);
            }
            xml.writeEndElement(); xml.writeEndDocument(); xml.close();
        }
        Files.move(temp.toPath(), snapshot.toPath(), StandardCopyOption.REPLACE_EXISTING);
    }
}
