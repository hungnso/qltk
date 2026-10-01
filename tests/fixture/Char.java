public final class Char {
    static final Char character = new Char();
    public String cName;
    public int clevel = 51, xu = 3456, luong = 78, xuInBox;
    public Object[] arrItemBox;
    public Item[] arrItemBody = { null, new Item() };
    public static Char getMyChar() { return character; }
}
