public final class ItemTemplates {
    public static int knownLookups;
    public static ItemTemplate get(short id) {
        if (id != 123 && id != 456) return null;
        knownLookups++;
        ItemTemplate template = new ItemTemplate(); template.id = id;
        if (id == 456) template.name = "Absent item";
        return template;
    }
}
