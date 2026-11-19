package com.plinko.updater;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInstaller;
import android.util.Log;

/**
 * Recibe el resultado de la sesión de instalación. Si Android necesita que el usuario confirme
 * (primera actualización, o versiones antiguas de Android), abre la pantalla de confirmación.
 */
public class InstallReceiver extends BroadcastReceiver {
    @Override
    public void onReceive(Context context, Intent intent) {
        int status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE);
        if (status == PackageInstaller.STATUS_PENDING_USER_ACTION) {
            Intent confirm = intent.getParcelableExtra(Intent.EXTRA_INTENT);
            if (confirm != null) {
                confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                context.startActivity(confirm);
            }
        } else if (status != PackageInstaller.STATUS_SUCCESS) {
            Log.w("PlinkoUpdater", "Instalación fallida: " + status + " " +
                    intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE));
            UpdaterBridge.lastError = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE);
        }
    }
}
