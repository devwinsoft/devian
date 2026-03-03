package com.devian.share;

import android.app.Activity;
import android.app.FragmentTransaction;
import android.content.Intent;
import android.net.Uri;
import androidx.core.content.FileProvider;
import java.io.File;

/**
 * OS 공유 시트 / 이메일을 통해 파일을 공유하고, 파일 선택 다이얼로그를 연다.
 * RecoveryManager (C#) → AndroidJavaClass 브릿지로 호출.
 */
public class DevianShare {

    private static final String FRAGMENT_TAG = "DevianFilePickerFragment";

    /**
     * OS 파일 선택 다이얼로그를 열어 사용자가 파일을 선택하게 한다.
     * 결과는 UnitySendMessage("RecoveryManager", "OnFilePickerResult", filePath)로 전달된다.
     *
     * @param activity  현재 Activity (UnityPlayer.currentActivity)
     */
    public static void pickFile(Activity activity) {
        if (activity == null) return;

        DevianFilePickerFragment fragment = new DevianFilePickerFragment();
        FragmentTransaction ft = activity.getFragmentManager().beginTransaction();
        ft.add(fragment, FRAGMENT_TAG);
        ft.commitAllowingStateLoss();
    }

    /**
     * OS 범용 공유 시트를 열어 파일을 공유한다.
     *
     * @param activity  현재 Activity (UnityPlayer.currentActivity)
     * @param filePath  공유할 파일의 절대 경로
     * @param subject   공유 시트 제목 (이메일 제목 등)
     */
    public static void shareFile(Activity activity, String filePath, String subject) {
        if (activity == null || filePath == null) return;

        File file = new File(filePath);
        if (!file.exists()) return;

        String authority = activity.getPackageName() + ".devianfileprovider";
        Uri contentUri = FileProvider.getUriForFile(activity, authority, file);

        Intent intent = new Intent(Intent.ACTION_SEND);
        intent.setType("application/octet-stream");
        intent.putExtra(Intent.EXTRA_STREAM, contentUri);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

        if (subject != null) {
            intent.putExtra(Intent.EXTRA_SUBJECT, subject);
        }

        activity.startActivity(Intent.createChooser(intent, subject));
    }

    /**
     * 이메일 앱을 열어 파일을 첨부하고 수신자를 프리셋한다.
     *
     * @param activity  현재 Activity (UnityPlayer.currentActivity)
     * @param filePath  첨부할 파일의 절대 경로
     * @param recipient 수신자 이메일 주소
     * @param subject   이메일 제목
     */
    public static void sendEmail(Activity activity, String filePath, String recipient, String subject) {
        if (activity == null || filePath == null || recipient == null) return;

        File file = new File(filePath);
        if (!file.exists()) return;

        String authority = activity.getPackageName() + ".devianfileprovider";
        Uri contentUri = FileProvider.getUriForFile(activity, authority, file);

        Intent intent = new Intent(Intent.ACTION_SEND);
        intent.setType("message/rfc822");
        intent.putExtra(Intent.EXTRA_EMAIL, new String[]{ recipient });
        intent.putExtra(Intent.EXTRA_STREAM, contentUri);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

        if (subject != null) {
            intent.putExtra(Intent.EXTRA_SUBJECT, subject);
        }

        activity.startActivity(Intent.createChooser(intent, subject));
    }
}
