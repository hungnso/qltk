import java.io.*;
import java.util.*;
import javax.xml.parsers.*;
import javax.xml.stream.*;
import org.w3c.dom.*;

/** Reads inventory on the game event thread; never changes items. */
public final class ItemStatistics {
    private final File config;
    private boolean showEquipment, showItems, equipmentKnown, bagKnown, boxKnown;
    private final List<Entry> equipment = new ArrayList<Entry>();
    private final LinkedHashMap<Integer, Entry> items = new LinkedHashMap<Integer, Entry>();
    private Element cachedConfig;
    private long configTime, configLength, lastConfigRead;
    private ClassLoader nameLoader;
    private final Map<Integer, String> names = new HashMap<Integer, String>();
    private volatile boolean light = Boolean.parseBoolean(System.getenv("QLTK_VPS_LIGHT"));
    public boolean isLight() { return light; }
    private static final class Entry {
        int id, upgrade; String name; long quantity;
        Entry(int id, String name) { this.id = id; this.name = name; }
    }
    public ItemStatistics(File config) { this.config = config; }
    private static String text(Element root, String name) {
        NodeList nodes = root.getElementsByTagName(name);
        return nodes.getLength() == 0 ? "" : nodes.item(0).getTextContent().trim();
    }
    public void read(ClassLoader loader, Class<?> type, Object character) {
        equipment.clear(); items.clear(); equipmentKnown = bagKnown = boxKnown = false;
        showEquipment = showItems = false;
        if (config == null || !config.isFile()) return;
        try {
            if (config.length() > 262144) return;
            if (cachedConfig == null || configTime != config.lastModified() || configLength != config.length() || System.currentTimeMillis() - lastConfigRead >= 10000) {
                DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
                factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
                factory.setFeature("http://xml.org/sax/features/external-general-entities", false);
                factory.setFeature("http://xml.org/sax/features/external-parameter-entities", false);
                factory.setXIncludeAware(false); factory.setExpandEntityReferences(false);
                long stamp = config.lastModified(), length = config.length();
                cachedConfig = factory.newDocumentBuilder().parse(config).getDocumentElement();
                configTime = stamp; configLength = length; lastConfigRead = System.currentTimeMillis();
            }
            Element root = cachedConfig;
            String mode = text(root, "VpsLight"); if (!mode.isEmpty()) light = Boolean.parseBoolean(mode);
            if (nameLoader != loader) { names.clear(); nameLoader = loader; }
            showEquipment = text(root, "ShowUnder8").equalsIgnoreCase("true");
            showItems = text(root, "ShowTrackedItems").equalsIgnoreCase("true");
            if (showEquipment) {
                Object[] body = (Object[])type.getField("arrItemBody").get(character);
                if (body != null) {
                    for (Object item : body) if (item != null) {
                        Object template = item.getClass().getField("template").get(item);
                        if (template == null) throw new IOException("Equipment not loaded");
                        int itemType = template.getClass().getField("type").getByte(template) & 255;
                        int level = template.getClass().getField("level").getByte(template) & 255;
                        int upgrade = item.getClass().getField("upgrade").getInt(item);
                        // Game's normal upgrade UI accepts body types 0..9, level >=10.
                        if (itemType <= 9 && level >= 10 && upgrade >= 0 && upgrade < 8) {
                            Entry entry = entry(template); entry.upgrade = upgrade; equipment.add(entry);
                        }
                    }
                    equipmentKnown = true;
                }
            }
            if (showItems) {
                String ids = text(root, "TrackedItemIds");
                if (!ids.isEmpty()) for (String token : ids.split(",", -1)) {
                    int id = Integer.parseInt(token.trim());
                    if (id < 0 || id > 32767 || items.size() >= 128 && !items.containsKey(id)) throw new IOException("Invalid item IDs");
                    items.put(id, new Entry(id, "ID " + id));
                }
                for (Entry entry : items.values()) {
                    if (names.containsKey(entry.id)) { entry.name = names.get(entry.id); continue; }
                    try {
                        Object template = Class.forName("ItemTemplates", true, loader).getMethod("get", short.class).invoke(null, (short)entry.id);
                        if (template != null) { entry.name = entry(template).name; names.put(entry.id, entry.name); }
                    } catch (Exception ignored) { }
                }
                Object[] bag = (Object[])type.getField("arrItemBag").get(character);
                Object[] box = (Object[])type.getField("arrItemBox").get(character);
                bagKnown = add(bag); boxKnown = add(box);
            }
        } catch (Exception e) { equipmentKnown = bagKnown = boxKnown = false; equipment.clear(); items.clear(); }
    }
    private static Entry entry(Object template) throws Exception {
        int id = template.getClass().getField("id").getShort(template);
        String name = (String)template.getClass().getField("name").get(template);
        return new Entry(id, name == null || name.trim().isEmpty() ? "ID " + id : name);
    }
    private boolean add(Object[] contents) throws Exception {
        if (contents == null) return false;
        boolean known = true;
        for (Object item : contents) if (item != null) {
            Object template = item.getClass().getField("template").get(item);
            if (template == null) { known = false; continue; }
            Entry source = entry(template), target = items.get(source.id);
            if (target != null) {
                boolean stackable = template.getClass().getField("isUpToUp").getBoolean(template);
                int quantity = item.getClass().getField("quantity").getInt(item);
                if (quantity < 0) { known = false; continue; }
                target.quantity += stackable ? Math.max(1, quantity) : 1;
                target.name = source.name;
                names.put(source.id, source.name);
            }
        }
        return known;
    }
    public void write(XMLStreamWriter xml) throws Exception {
        if (showEquipment) {
            xml.writeStartElement("Equipment"); xml.writeAttribute("known", String.valueOf(equipmentKnown));
            if (equipmentKnown) for (Entry item : equipment) {
                xml.writeEmptyElement("Item"); xml.writeAttribute("name", item.name); xml.writeAttribute("upgrade", String.valueOf(item.upgrade));
            }
            xml.writeEndElement();
        }
        if (showItems) {
            xml.writeStartElement("Inventory"); xml.writeAttribute("bagKnown", String.valueOf(bagKnown)); xml.writeAttribute("boxKnown", String.valueOf(boxKnown));
            for (Entry item : items.values()) {
                xml.writeEmptyElement("Item"); xml.writeAttribute("id", String.valueOf(item.id)); xml.writeAttribute("name", item.name); xml.writeAttribute("quantity", String.valueOf(item.quantity));
            }
            xml.writeEndElement();
        }
    }
}
