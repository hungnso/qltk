import java.util.*;
import javax.tools.*;

/** javac's Windows zipfs cleanup can fail under a restricted filesystem.
 * Keep the file manager alive until this short-lived compiler process exits. */
public final class CompileJava {
    public static void main(String[] args) {
        JavaCompiler compiler = ToolProvider.getSystemJavaCompiler();
        if (compiler == null || args.length < 3) { System.exit(2); return; }
        StandardJavaFileManager files = compiler.getStandardFileManager(null, null, java.nio.charset.StandardCharsets.UTF_8);
        Iterable<? extends JavaFileObject> sources = files.getJavaFileObjectsFromStrings(Arrays.asList(args).subList(2, args.length));
        boolean ok = compiler.getTask(null, files, null, Arrays.asList("--release", "8", "-Xlint:-options", "-encoding", "UTF-8", "-cp", args[0], "-d", args[1]), null, sources).call();
        System.exit(ok ? 0 : 1);
    }
}
