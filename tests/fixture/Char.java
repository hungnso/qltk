public final class Char {
    public static boolean timeStartBlink, isAHP, isAMP, isAFood, isABuff, isAResuscitate, isAPickYen, isAPickYHM, isAPickYHMS, dm, dn, doa, dp, dq, dr, ds, dt, du, dv, dw, dx, dy, dz, ea, eb, ec, ed, ee, ef, eg, eh;
    public static int ek = 50, el = 20, em = 30, en = 30, eo = 5, ep = 7, eq = 30, saves;
    public static boolean failSave;
    public static void b() {
        saves++;
        if (failSave) return;
        try {
            java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
            java.io.DataOutputStream data = new java.io.DataOutputStream(bytes);
            data.writeBoolean(timeStartBlink); data.writeInt(ek);
            data.writeBoolean(isAHP); data.writeInt(el);
            data.writeBoolean(isAMP); data.writeInt(em);
            for (boolean value : new boolean[] {isAFood,isABuff,isAResuscitate,isAPickYen,isAPickYHM,isAPickYHMS}) data.writeBoolean(value);
            data.writeInt(en); data.writeBoolean(dm); data.writeInt(eo); data.writeBoolean(dn); data.writeInt(ep); data.writeBoolean(doa); data.writeInt(eq);
            for (boolean value : new boolean[] {dp,dq,dr,ds,dt,du,dv,dw,dx,dy,dz,eh,ea,eb,ec,ed,ee,ef,eg}) data.writeBoolean(value);
            data.close(); mResources.a("V7LCSetting", bytes.toByteArray());
            java.nio.file.Files.write(new java.io.File(System.getProperty("user.home"), "auto-saved.txt").toPath(), (isAFood + ":" + ek + ":" + doa).getBytes("UTF-8"));
        }
        catch (java.io.IOException e) { throw new IllegalStateException(e); }
    }
    static final Char character = new Char();
    public String cName;
    public int clevel = 51, xu = 3456, luong = 78, xuInBox;
    public Object[] arrItemBox;
    public Item[] arrItemBag = { new Item(), new Item() };
    public Item[] arrItemBody = { null, new Item() };
    public static Char getMyChar() { return character; }
}
