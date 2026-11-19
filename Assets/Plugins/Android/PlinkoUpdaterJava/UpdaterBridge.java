package com.plinko.updater;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;

/** Funciones que llama el AutoUpdater de C# para instalar el APK descargado. */
public final class UpdaterBridge {
    private UpdaterBridge() {}

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
