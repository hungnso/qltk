public final class LoginScr {
    public static LoginScr mgI() { return (LoginScr) GameCanvas.currentScreen; }
    public void autoLogin(String user, String password) {
        if (!user.equals("fixture-user") || !password.equals(" fixture-pass ")) throw new IllegalStateException("Wrong login credentials.");
        if (!GameMidlet.g.equals("fixture2") || GameMidlet.port != 1002 || GameMidlet.serverLogin != 2 || GameCanvas.menu.menuSelectedItem != 2 || mResources.selected != 2) throw new IllegalStateException("Wrong server.");
        GameCanvas.currentScreen = new SelectCharScr();
    }
}
