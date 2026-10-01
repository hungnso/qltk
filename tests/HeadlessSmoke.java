import java.io.*;
import java.nio.file.*;
import org.microemu.app.Headless;
import org.microemu.MIDletBridge;
import javax.microedition.midlet.MIDlet;
import javax.microedition.lcdui.Display;
import javax.xml.parsers.DocumentBuilderFactory;
import org.w3c.dom.Element;
public final class HeadlessSmoke {
    public static void main(String[] args) throws Exception {
        File game = new File(args[0]), home = new File(args[1]);
        home.mkdirs(); File status = new File(home, "bridge.status");
        AccountBootstrap.verifyContract(game);
        AccountBootstrap.prepare(home, game, "fixture-user", " fixture-pass ", 2);
        System.setProperty("user.home", home.getAbsolutePath());
        System.setProperty("java.awt.headless", "true");
        Headless.main(new String[] { "--rms", "file", "--openjar", game.getAbsolutePath() });
        AccountBootstrap.loginWhenReady("fixture-user", " fixture-pass ", 2, status);
        String value = new String(Files.readAllBytes(status.toPath()), "UTF-8");
        if (!value.equals("LOGIN_SUBMITTED")) { System.err.println("FAIL: headless emulator integration: " + value); System.exit(1); }
        System.out.println("PASS: real emulator starts fixture and submits correct account/password/server on its event thread (offline).");
        MIDlet midlet = MIDletBridge.getCurrentMIDlet();
        final ClassLoader loader = midlet.getClass().getClassLoader();
        final GameAccountObserver observer = new GameAccountObserver(status, true);
        tick(midlet, observer, loader);
        Class<?> selector = Class.forName("SelectCharScr", false, loader);
        if (selector.getField("selections").getInt(null) != 1) throw new AssertionError("First slot not automatically chosen.");
        tick(midlet, observer, loader);
        File info = new File(home, "character.xml");
        Element snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("name").equals("first-character") || !snapshot.getAttribute("xu").equals("3456") || !snapshot.getAttribute("luong").equals("78")) throw new AssertionError("Wrong character currency snapshot.");
        if (!snapshot.getAttribute("weaponState").equals("EQUIPPED") || !snapshot.getAttribute("weaponLevel").equals("50") || !snapshot.getAttribute("weaponUpgrade").equals("12")) throw new AssertionError("Equipped weapon info is missing.");
        if (!snapshot.getAttribute("boxKnown").equals("false") || snapshot.hasAttribute("boxXu")) throw new AssertionError("Unloaded chest must not be shown as zero.");
        Class<?> characterType = Class.forName("Char", false, loader);
        Object character = characterType.getMethod("getMyChar").invoke(null);
        characterType.getField("arrItemBox").set(character, new Object[0]);
        characterType.getField("xuInBox").setInt(character, 99);
        tick(midlet, observer, loader);
        snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("boxKnown").equals("true") || !snapshot.getAttribute("boxXu").equals("99")) throw new AssertionError("Server chest response not reflected.");
        characterType.getField("xu").setInt(character, 0);
        tick(midlet, observer, loader);
        snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("xu").equals("0")) throw new AssertionError("Loaded zero balance must stay valid.");
        Object[] equipment = (Object[])characterType.getField("arrItemBody").get(character);
        Object weapon = equipment[1], template = weapon.getClass().getField("template").get(weapon);
        template.getClass().getField("level").setByte(template, (byte)130);
        tick(midlet, observer, loader);
        snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("weaponLevel").equals("130")) throw new AssertionError("Weapon level byte must be read unsigned.");
        equipment[1] = null;
        tick(midlet, observer, loader);
        snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("weaponState").equals("NONE") || snapshot.hasAttribute("weaponLevel")) throw new AssertionError("Empty weapon slot must not show old equipment.");
        characterType.getField("arrItemBody").set(character, null);
        tick(midlet, observer, loader);
        snapshot = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(info).getDocumentElement();
        if (!snapshot.getAttribute("weaponState").equals("UNKNOWN")) throw new AssertionError("Unloaded equipment must remain unknown.");
        if (Class.forName("Service", false, loader).getField("requests").getInt(null) != 1) throw new AssertionError("Must not flood server with chest requests.");
        Object emptySelector = selector.newInstance();
        selector.getField("name").set(emptySelector, new String[] { null, "second-character", null });
        Class.forName("GameCanvas", false, loader).getField("currentScreen").set(null, emptySelector);
        tick(midlet, observer, loader);
        if (selector.getField("selections").getInt(null) != 1) throw new AssertionError("Empty first slot must not select or create another character.");
        if (!new String(Files.readAllBytes(status.toPath()), "UTF-8").equals("CHARACTER_SLOT_EMPTY")) throw new AssertionError("Empty first slot must be reported.");
        System.out.println("PASS: slot one selected once, character and balances read, delayed chest stays unknown, response updates, empty slot does not create a character.");
        System.exit(0);
    }
    static void tick(MIDlet midlet, final GameAccountObserver observer, final ClassLoader loader) throws Exception {
        final java.util.concurrent.CountDownLatch done = new java.util.concurrent.CountDownLatch(1);
        final java.util.concurrent.atomic.AtomicReference<Throwable> error = new java.util.concurrent.atomic.AtomicReference<Throwable>();
        Display.getDisplay(midlet).callSerially(new Runnable() { public void run() {
            try { MIDletBridge.setThreadMIDletContext(MIDletBridge.getMIDletContext(MIDletBridge.getCurrentMIDlet())); observer.tick(loader); }
            catch (Throwable e) { error.set(e); } finally { done.countDown(); }
        } });
        if (!done.await(5, java.util.concurrent.TimeUnit.SECONDS)) throw new AssertionError("Game event thread did not run.");
        if (error.get() != null) throw new AssertionError(error.get());
    }
}
