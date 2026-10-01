public final class Service {
    static final Service instance = new Service();
    public static int requests;
    public static Service gI() { return instance; }
    public void requestItem(int type) {
        if (type != 4) throw new IllegalStateException("Must request chest read, not another action.");
        requests++;
        // Delayed server response: a missing response must not become zero.
    }
}
