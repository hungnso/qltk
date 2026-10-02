import java.io.*;
import java.lang.reflect.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;
import javax.xml.parsers.*;
import javax.xml.stream.*;
import org.w3c.dom.*;

/** Reads and changes the game Auto menu on MicroEmulator's event thread. */
public final class GameAutoController {
    static final String[] FIELDS = {"timeStartBlink","isAHP","isAMP","isAFood","isABuff","isAResuscitate","isAPickYen","isAPickYHM","isAPickYHMS","dm","dn","doa","dp","dq","dr","ds","dt","du","dv","dw","dx","dy","dz","ea","eb","ec","ed","ee","ef","eg","eh","weaponOnlyPickup"};
    static final String[] VALUES = {"ek","el","em","en","eo","ep","eq"};
    private final File state, command, result;
    private final String session;
    private byte[] lastState;
    private long lastRequest;
    private long pendingResultId;
    private String pendingOutcome, pendingMessage;
    private static final XMLOutputFactory XML = XMLOutputFactory.newFactory();
    public GameAutoController(File home, String session) {
        if (home == null || session == null || session.length() == 0 || session.length() > 128) throw new IllegalArgumentException("Invalid Auto session");
        this.session = session;
        state = new File(home, "auto-state.xml"); command = new File(home, "auto-command.xml"); result = new File(home, "auto-result.xml");
    }
    public void tick(ClassLoader loader) {
        try {
            Map<String,String> current = read(loader);
            if (pendingResultId == 0 && command.isFile() && command.length() <= 65536) {
                try {
                    Document request = parse(command);
                    Element root = request.getDocumentElement();
                    if (!root.getTagName().equals("AutoCommand")) throw new IOException("Invalid Auto command");
                    if (session.equals(root.getAttribute("session"))) {
                        long id = Long.parseLong(root.getAttribute("id"));
                        if (id > lastRequest) {
                            lastRequest = id;
                            pendingResultId = id;
                            try { apply(loader, root, current); pendingOutcome = "OK"; pendingMessage = ""; }
                            catch (Exception e) { pendingOutcome = "ERROR"; pendingMessage = safeMessage(e); }
                        }
                    }
                } catch (Exception ignored) { /* A partially written or old command cannot affect the game. */ }
            }
            if (pendingResultId != 0) {
                try { writeResult(pendingResultId, pendingOutcome, pendingMessage); pendingResultId = 0; }
                catch (Exception ignored) { /* Retry delivery on the next observer tick without applying twice. */ }
            }
            writeState(read(loader), "READY");
        } catch (Exception e) {
            try { writeState(null, "UNSUPPORTED"); } catch (Exception ignored) { }
        }
    }
    public void waiting() {
        try { writeState(null, "WAITING"); } catch (Exception ignored) { }
    }
    public static Map<String,String> read(ClassLoader loader) throws Exception {
        Class<?> character = Class.forName("Char", true, loader);
        Class<?> daily = Class.forName("AutoDailyPanel", true, loader);
        Map<String,String> values = new LinkedHashMap<String,String>();
        for (String key : FIELDS) values.put(key, Boolean.toString((key.equals("weaponOnlyPickup") ? daily : character).getField(key).getBoolean(null)));
        for (String key : VALUES) values.put(key, Integer.toString(character.getField(key).getInt(null)));
        character.getMethod("b");
        daily.getDeclaredMethod("setWeaponOnlyPickup", boolean.class, boolean.class).setAccessible(true);
        return values;
    }
    private void apply(ClassLoader loader, Element root, Map<String,String> old) throws Exception {
        Map<String,String> changes = new LinkedHashMap<String,String>();
        NodeList sets = root.getChildNodes();
        for (int i = 0; i < sets.getLength(); i++) {
            if (!(sets.item(i) instanceof Element)) continue;
            Element element = (Element)sets.item(i);
            if (!element.getTagName().equals("Set")) throw new IOException("Invalid Auto field");
            String key = element.getAttribute("key"), value;
            if (!old.containsKey(key) || changes.containsKey(key)) throw new IOException("Invalid Auto field: " + key);
            if (Arrays.asList(FIELDS).contains(key)) {
                value = element.getAttribute("enabled");
                if (!value.equals("true") && !value.equals("false") || element.hasAttribute("value")) throw new IOException("Invalid Auto checkbox: " + key);
            } else {
                value = element.getAttribute("value");
                if (element.hasAttribute("enabled") || !validNumber(key, value)) throw new IOException("Invalid Auto number: " + key);
            }
            changes.put(key, value);
        }
        if (changes.size() == 0) throw new IOException("Empty Auto command");
        String state = changes.get("dt");
        if ("true".equals(state)) for (String key : new String[] {"isAPickYHM","isAPickYHMS","dm","doa","dp","dq","dr","ds","weaponOnlyPickup"}) changes.put(key, "false");
        else {
            if ("true".equals(changes.get("weaponOnlyPickup"))) { changes.put("doa", "false"); changes.put("dt", "false"); }
            else if ("true".equals(changes.get("doa"))) { changes.put("weaponOnlyPickup", "false"); changes.put("dt", "false"); }
            for (String key : new String[] {"isAPickYHM","isAPickYHMS","dm","dp","dq","dr","ds"}) if ("true".equals(changes.get(key))) changes.put("dt", "false");
        }
        Class<?> character = Class.forName("Char", true, loader), daily = Class.forName("AutoDailyPanel", true, loader);
        try {
            setFields(character, daily, changes);
            character.getMethod("b").invoke(null);
            Map<String,String> live = read(loader), saved = readSaved(loader);
            for (Map.Entry<String,String> entry : changes.entrySet()) {
                if (!entry.getValue().equals(live.get(entry.getKey())) || !entry.getValue().equals(saved.get(entry.getKey()))) throw new IOException("Game did not save Auto value: " + entry.getKey());
            }
        } catch (Exception e) {
            try { setFields(character, daily, old); character.getMethod("b").invoke(null); } catch (Exception ignored) { }
            throw e;
        }
    }
    private static void setFields(Class<?> character, Class<?> daily, Map<String,String> changes) throws Exception {
        if (changes.containsKey("weaponOnlyPickup")) {
            boolean requested = Boolean.parseBoolean(changes.get("weaponOnlyPickup"));
            if (daily.getField("weaponOnlyPickup").getBoolean(null) != requested) {
                Method setter = daily.getDeclaredMethod("setWeaponOnlyPickup", boolean.class, boolean.class);
                setter.setAccessible(true); setter.invoke(null, requested, false);
            }
        }
        for (Map.Entry<String,String> entry : changes.entrySet()) {
            if (entry.getKey().equals("weaponOnlyPickup")) continue;
            Field field = character.getField(entry.getKey());
            if (field.getType() == boolean.class) field.setBoolean(null, Boolean.parseBoolean(entry.getValue()));
            else field.setInt(null, Integer.parseInt(entry.getValue()));
        }
    }
    private static Map<String,String> readSaved(ClassLoader loader) throws Exception {
        Class<?> resources = Class.forName("mResources", true, loader);
        byte[] bytes = (byte[])resources.getMethod("b", String.class).invoke(null, "V7LCSetting");
        if (bytes == null || bytes.length > 262144) throw new IOException("Game did not save Auto RMS");
        Map<String,String> values = new HashMap<String,String>();
        DataInputStream input = new DataInputStream(new ByteArrayInputStream(bytes));
        values.put("timeStartBlink", Boolean.toString(input.readBoolean())); values.put("ek", Integer.toString(input.readInt()));
        values.put("isAHP", Boolean.toString(input.readBoolean())); values.put("el", Integer.toString(input.readInt()));
        values.put("isAMP", Boolean.toString(input.readBoolean())); values.put("em", Integer.toString(input.readInt()));
        for (String key : new String[] {"isAFood","isABuff","isAResuscitate","isAPickYen","isAPickYHM","isAPickYHMS"}) values.put(key, Boolean.toString(input.readBoolean()));
        values.put("en", Integer.toString(input.readInt())); values.put("dm", Boolean.toString(input.readBoolean())); values.put("eo", Integer.toString(input.readInt())); values.put("dn", Boolean.toString(input.readBoolean())); values.put("ep", Integer.toString(input.readInt())); values.put("doa", Boolean.toString(input.readBoolean())); values.put("eq", Integer.toString(input.readInt()));
        for (String key : new String[] {"dp","dq","dr","ds","dt","du","dv","dw","dx","dy","dz","eh","ea","eb","ec","ed","ee","ef","eg"}) values.put(key, Boolean.toString(input.readBoolean()));
        int daily = (Integer)resources.getMethod("d", String.class).invoke(null, "AutoDailyOptions");
        values.put("weaponOnlyPickup", Boolean.toString(daily >= 0 && (daily & 16) != 0));
        return values;
    }
    private static boolean validNumber(String key, String value) {
        int number;
        try { if (value.length() == 0 || !value.matches("[0-9]+")) return false; number = Integer.parseInt(value); }
        catch (Exception e) { return false; }
        if (key.equals("ek")) return number >= 1 && number <= 99;
        if (key.equals("el")) return number >= 10 && number <= 90;
        if (key.equals("eo")) return number >= 1 && number <= 7;
        if (key.equals("ep")) return number >= 1 && number <= 12;
        return number == 1 || number >= 10 && number <= 70 && number % 10 == 0;
    }
    private void writeState(Map<String,String> values, String availability) throws Exception {
        ByteArrayOutputStream out = new ByteArrayOutputStream(); XMLStreamWriter xml = XML.createXMLStreamWriter(out, "UTF-8");
        xml.writeStartElement("AutoState"); xml.writeAttribute("session", session); xml.writeAttribute("state", availability);
        if (values != null) for (int i = 0; i < FIELDS.length; i++) {
            xml.writeEmptyElement("Option"); xml.writeAttribute("key", FIELDS[i]); xml.writeAttribute("enabled", values.get(FIELDS[i]));
            if (i == 0) xml.writeAttribute("value", values.get("ek"));
            else if (i == 1) xml.writeAttribute("value", values.get("el"));
            else if (i == 2) xml.writeAttribute("value", values.get("em"));
            else if (i == 8) xml.writeAttribute("value", values.get("en"));
            else if (i == 9) xml.writeAttribute("value", values.get("eo"));
            else if (i == 10) xml.writeAttribute("value", values.get("ep"));
            else if (i == 11) xml.writeAttribute("value", values.get("eq"));
        }
        xml.writeEndElement(); xml.close(); byte[] data = out.toByteArray();
        if (Arrays.equals(lastState, data) && state.isFile()) return;
        atomic(state, data); lastState = data;
    }
    private void writeResult(long id, String outcome, String message) throws Exception {
        ByteArrayOutputStream out = new ByteArrayOutputStream(); XMLStreamWriter xml = XML.createXMLStreamWriter(out, "UTF-8");
        xml.writeStartElement("AutoResult"); xml.writeAttribute("session", session); xml.writeAttribute("id", Long.toString(id)); xml.writeAttribute("state", outcome); xml.writeAttribute("message", message); xml.writeEndElement(); xml.close();
        atomic(result, out.toByteArray());
    }
    private static String safeMessage(Exception e) { return e instanceof IOException ? e.getMessage() : "Game Auto không hỗ trợ thao tác này"; }
    private static void atomic(File path, byte[] data) throws IOException {
        File temp = new File(path.getParentFile(), path.getName() + ".tmp");
        Files.write(temp.toPath(), data);
        Files.move(temp.toPath(), path.toPath(), StandardCopyOption.REPLACE_EXISTING);
    }
    private static Document parse(File path) throws Exception {
        DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
        factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
        factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
        factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
        factory.setXIncludeAware(false); factory.setExpandEntityReferences(false);
        DocumentBuilder builder = factory.newDocumentBuilder();
        builder.setErrorHandler(new org.xml.sax.ErrorHandler() {
            public void warning(org.xml.sax.SAXParseException e) throws org.xml.sax.SAXException { throw e; }
            public void error(org.xml.sax.SAXParseException e) throws org.xml.sax.SAXException { throw e; }
            public void fatalError(org.xml.sax.SAXParseException e) throws org.xml.sax.SAXException { throw e; }
        });
        return builder.parse(path);
    }
}
