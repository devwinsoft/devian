package com.devian.share;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentTransaction;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.DocumentsContract;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

/**
 * OS 파일 선택 다이얼로그를 열어 파일을 선택받는 headless Fragment.
 * DevianShare.pickFile(activity) → Fragment 추가 → ACTION_OPEN_DOCUMENT → 결과 → UnitySendMessage
 *
 * NativeFilePicker(yasirkula) 패턴 참고.
 */
public class DevianFilePickerFragment extends Fragment {

    private static final String TAG = "DevianFilePickerFragment";
    private static final int REQUEST_CODE_PICK_FILE = 59001;

    private boolean intentLaunched = false;

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setRetainInstance(true);
    }

    @Override
    public void onResume() {
        super.onResume();

        if (!intentLaunched) {
            intentLaunched = true;
            launchPicker();
        }
    }

    private void launchPicker() {
        try {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.setType("application/octet-stream");
            intent.addCategory(Intent.CATEGORY_OPENABLE);

            // API 26+: Downloads 폴더를 초기 위치로 힌트
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                Uri downloadsUri = Uri.parse("content://com.android.providers.downloads.documents/document/downloads");
                intent.putExtra(DocumentsContract.EXTRA_INITIAL_URI, downloadsUri);
            }

            startActivityForResult(intent, REQUEST_CODE_PICK_FILE);
        } catch (Exception e) {
            sendResult("");
            removeSelf();
        }
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        if (requestCode != REQUEST_CODE_PICK_FILE) {
            super.onActivityResult(requestCode, resultCode, data);
            return;
        }

        if (resultCode == Activity.RESULT_OK && data != null && data.getData() != null) {
            Uri uri = data.getData();
            String tempPath = copyToTempFile(uri);
            sendResult(tempPath != null ? tempPath : "");
        } else {
            // 사용자 취소
            sendResult("");
        }

        removeSelf();
    }

    private String copyToTempFile(Uri uri) {
        Activity activity = getActivity();
        if (activity == null) return null;

        InputStream inputStream = null;
        FileOutputStream outputStream = null;
        try {
            inputStream = activity.getContentResolver().openInputStream(uri);
            if (inputStream == null) return null;

            long timestamp = System.currentTimeMillis();
            File tempFile = new File(activity.getCacheDir(), "recovery_pick_" + timestamp + ".dvn");

            outputStream = new FileOutputStream(tempFile);
            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = inputStream.read(buffer)) != -1) {
                outputStream.write(buffer, 0, bytesRead);
            }
            outputStream.flush();

            return tempFile.getAbsolutePath();
        } catch (Exception e) {
            return null;
        } finally {
            try { if (inputStream != null) inputStream.close(); } catch (Exception ignored) {}
            try { if (outputStream != null) outputStream.close(); } catch (Exception ignored) {}
        }
    }

    private String getGameObjectName() {
        android.os.Bundle args = getArguments();
        return (args != null) ? args.getString("gameObjectName", "RecoveryManager") : "RecoveryManager";
    }

    private void sendResult(String filePath) {
        try {
            com.unity3d.player.UnityPlayer.UnitySendMessage(
                getGameObjectName(), "OnFilePickerResult", filePath);
        } catch (Exception ignored) {}
    }

    private void removeSelf() {
        try {
            Activity activity = getActivity();
            if (activity == null || activity.isFinishing()) return;

            FragmentTransaction ft = activity.getFragmentManager().beginTransaction();
            ft.remove(this);
            ft.commitAllowingStateLoss();
        } catch (Exception ignored) {}
    }
}
