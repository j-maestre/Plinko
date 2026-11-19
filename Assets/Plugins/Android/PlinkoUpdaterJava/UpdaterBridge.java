package com.plinko.updater;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.Intent;
import android.content.pm.PackageInstaller;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

import java.io.File;
import java.io.FileInputStream;
import java.io.InputStream;
import java.io.OutputStream;

/** Funciones que llama el AutoUpdater de C# para instalar el APK descargado. */
public final class UpdaterBridge {
    private UpdaterBridge() {}

    /** Último error devuelto por el instalador (lo lee C# para mostrarlo). */
    public static volatile String lastError;

    public static String takeLastError() {
        String e = lastError;
        lastError = null;
        return e;
    }

    /**
     * Instala con PackageInstaller. En Android 12+ pide no requerir acción del usuario:
     * si la app ya es la "instaladora" de sí misma (a partir de la primera actualización), se instala sola.
     * Si Android exige confirmación, InstallReceiver abre la pantalla del sistema.
     */
    public static void installSession(Activity activity, String apkPath) throws Exception {
        PackageInstaller installer = activity.getPackageManager().getPackageInstaller();
        PackageInstaller.SessionParams params =
                new PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL);
        params.setAppPackageName(activity.getPackageName());
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S)
            params.setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED);

        int sessionId = installer.createSession(params);
        PackageInstaller.Session session = installer.openSession(sessionId);
        try {
            File file = new File(apkPath);
            try (InputStream in = new FileInputStream(file);
                 OutputStream out = session.openWrite("base.apk", 0, file.length())) {
                byte[] buffer = new byte[65536];
                int n;
                while ((n = in.read(buffer)) > 0) out.write(buffer, 0, n);
                session.fsync(out);
            }
            Intent intent = new Intent(activity, InstallReceiver.class);
            int flags = PendingIntent.FLAG_UPDATE_CURRENT;
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) flags |= PendingIntent.FLAG_MUTABLE;
            PendingIntent pending = PendingIntent.getBroadcast(activity, sessionId, intent, flags);
            session.commit(pending.getIntentSender());
        } finally {
            session.close();
        }
    }

    /** Carpeta donde C# debe guardar el APK (la misma que sirve ApkProvider). */
    public static String cacheDir(Activity activity) {
        return activity.getCacheDir().getAbsolutePath();
    }

    /** ¿Tiene la app permiso para instalar paquetes? (Android 8+ lo pide por app.) */
    public static boolean canInstall(Activity activity) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O)
            return activity.getPackageManager().canRequestPackageInstalls();
        return true;
    }

    /** Abre los ajustes para permitir "Instalar apps desconocidas" a este juego. */
    public static void openInstallPermission(Activity activity) {
        Intent intent = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:" + activity.getPackageName()));
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        activity.startActivity(intent);
    }

    /** Lanza el instalador del sistema con el APK de la caché. El usuario confirma la instalación. */
    public static void install(Activity activity, String fileName) {
        Uri uri = Uri.parse("content://" + activity.getPackageName() + ".apkprovider/" + fileName);
        Intent intent = new Intent(Intent.ACTION_VIEW);
        intent.setDataAndType(uri, "application/vnd.android.package-archive");
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
        activity.startActivity(intent);
    }

    /** Paquete del instalador (com.android.vending si viene de Google Play). */
    @SuppressWarnings("deprecation")
    public static String installerPackage(Activity activity) {
        try {
            PackageManager pm = activity.getPackageManager();
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R)
                return pm.getInstallSourceInfo(activity.getPackageName()).getInstallingPackageName();
            return pm.getInstallerPackageName(activity.getPackageName());
        } catch (Exception e) {
            return null;
        }
    }
}
