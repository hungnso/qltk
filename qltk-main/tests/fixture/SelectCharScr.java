public final class SelectCharScr {
    public int indexSelect = 1;
    public String[] name = { "first-character", "second-character", null };
    public static int selections;
    public void perform(int action, Object arg) {
        if (action != 1000 || indexSelect != 0 || name[0] == null) throw new IllegalStateException("Must choose character slot one.");
        selections++; Char.getMyChar().cName = name[0]; GameCanvas.currentScreen = new GameScr();
    }
}
