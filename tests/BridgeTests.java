import java.io.*;
import java.nio.file.*;
import java.util.jar.*;
import org.microemu.app.util.FileRecordStoreManager;
import org.microemu.util.RecordStoreImpl;

public class BridgeTests {
    static int assertions;
    static void check(boolean condition, String message) { assertions++; if (!condition) throw new AssertionError(message); }
    public static void main(String[] args) throws Exception {
        File home = Files.createTempDirectory("qltk-bridge-test-").toFile();
        try {
            File jar = new File(home, "test.jar");
            Manifest mf = new Manifest(); mf.getMainAttributes().putValue("Manifest-Version", "1.0"); mf.getMainAttributes().putValue("MIDlet-Name", "TestSuite");
            try (JarOutputStream out = new JarOutputStream(new FileOutputStream(jar), mf)) { }
            AccountBootstrap.prepare(home, jar, "alice", " p|a\\ss\u0111 ", 9);
            File suite = new File(home, ".microemulator/suite-TestSuite");
            check(new String(read(suite, "acc"), "UTF-8").equals("alice"), "RMS must contain selected account.");
            check(new String(read(suite, "pass"), "UTF-8").equals(" p|a\\ss\u0111 "), "RMS password must round-trip unchanged.");
            check(read(suite, "indServer")[0] == 9, "Server choice must be stored before game startup.");
            check(read(suite, "AutoAccountRotation_enabled")[0] == 0, "Game rotation must not override a tab's account.");
            File marker = new File(suite, "unrelated-data"); Files.write(marker.toPath(), new byte[] { 42 });
            AccountBootstrap.prepare(home, jar, "alice", "updated", 2);
            check(Files.readAllBytes(marker.toPath())[0] == 42, "Preparation must preserve other game data.");
            check(new String(read(suite, "pass"), "UTF-8").equals("updated"), "Edited passwords must replace previous credentials.");
            File status = new File(home, "bridge.status");
            AccountBootstrap.writeStatus(status, "READY");
            check(new String(Files.readAllBytes(status.toPath()), "UTF-8").equals("READY"), "Status must be readable by manager.");
            boolean rejected = false;
            Manifest unsafe = new Manifest(); unsafe.getMainAttributes().putValue("Manifest-Version", "1.0"); unsafe.getMainAttributes().putValue("MIDlet-Name", "../../outside");
            try (JarOutputStream out = new JarOutputStream(new FileOutputStream(jar), unsafe)) { }
            try { AccountBootstrap.prepare(home, jar, "alice", "p", 0); } catch (IOException expected) { rejected = true; }
            check(rejected, "Unsafe suite name must not escape account data folder.");
            if (args.length > 0) AccountBootstrap.verifyContract(new File(args[0]));
            System.out.println("PASS: " + assertions + " RMS, credentials, server and status assertions; game contract verified");
        } finally { delete(home); }
    }
    static byte[] read(File suite, String key) throws Exception {
        try (DataInputStream in = new DataInputStream(new FileInputStream(new File(suite, "vj" + key + ".rs")))) {
            RecordStoreImpl store = new RecordStoreImpl(new FileRecordStoreManager(), in); store.setOpen(true); return store.getRecord(1);
        }
    }
    static void delete(File file) { if (file.isDirectory()) for (File f : file.listFiles()) delete(f); file.delete(); }
}
