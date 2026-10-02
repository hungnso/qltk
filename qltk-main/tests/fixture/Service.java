public final class Service {
    static final Service instance = new Service();
    public static int requests;
    public static Service gI() { return instance; }
    public void requestItem(int type) {
        if (type != 4) throw new IllegalStateException("Must request chest read, not another action.");
        requests++;
        // Delayed server response: a missing response must not become zero.
    }
    public void login(String user, String password, String version) {
        if (!user.equals("fixture-user") || !password.equals(" fixture-pass ") || !version.equals("fixture-v37")) throw new IllegalStateException("Wrong v37 login credentials.");
        if (!GameMidlet.g.equals("fixture2") || GameMidlet.port != 1002 || GameMidlet.serverLogin != 2 || GameCanvas.menu.menuSelectedItem != 2 || mResources.selected != 2) throw new IllegalStateException("Wrong v37 server.");
        GameCanvas.currentScreen = new SelectCharScr();
    }
}
