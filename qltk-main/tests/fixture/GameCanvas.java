import javax.microedition.lcdui.Canvas;
import javax.microedition.lcdui.Graphics;
public final class GameCanvas extends Canvas {
    public static Object currentScreen = new LoginScr();
    public static Menu menu = new Menu();
    public static boolean isLoading;
    public static final class Menu { public int menuSelectedItem; }
    public void paint(Graphics graphics) { }
}
