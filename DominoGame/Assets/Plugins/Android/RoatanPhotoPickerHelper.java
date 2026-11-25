package com.roatandomino.game;

import android.app.Activity;
import android.app.Fragment;
import android.app.FragmentManager;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Bundle;
import android.provider.MediaStore;
import android.util.Base64;

import com.unity3d.player.UnityPlayer;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;

/**
 * Headless Fragment helper for picking a profile photo from the Android gallery
 * via Intent.ACTION_PICK / ACTION_GET_CONTENT, safely downsampling to 128x128 JPEG <= 32KB,
 * and returning the Base64 result to Unity.
 */
public class RoatanPhotoPickerHelper extends Fragment {
    private static final int REQUEST_PICK_IMAGE = 9421;
    private static final String FRAGMENT_TAG = "RoatanPhotoPickerFragment";
    private static final int TARGET_SIZE = 128;
    private static final int MAX_BYTES = 32 * 1024;

    private String targetGameObject;
    private String successMethod;
    private String errorMethod;

    public static void pickImage(final Activity activity, final String gameObject, final String onSuccess, final String onError) {
        if (activity == null) {
            UnityPlayer.UnitySendMessage(gameObject, onError, "Android activity unavailable.");
            return;
        }

        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    FragmentManager fm = activity.getFragmentManager();
                    RoatanPhotoPickerHelper existing = (RoatanPhotoPickerHelper) fm.findFragmentByTag(FRAGMENT_TAG);
                    if (existing != null) {
                        fm.beginTransaction().remove(existing).commitAllowingStateLoss();
                    }

                    RoatanPhotoPickerHelper fragment = new RoatanPhotoPickerHelper();
                    fragment.targetGameObject = gameObject;
                    fragment.successMethod = onSuccess;
                    fragment.errorMethod = onError;
                    fm.beginTransaction().add(fragment, FRAGMENT_TAG).commitAllowingStateLoss();
                } catch (Exception ex) {
                    UnityPlayer.UnitySendMessage(gameObject, onError, "Could not start photo picker: " + ex.getMessage());
                }
            }
        });
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        try {
            Intent pickIntent = new Intent(Intent.ACTION_PICK, MediaStore.Images.Media.EXTERNAL_CONTENT_URI);
            pickIntent.setType("image/*");
            if (pickIntent.resolveActivity(getActivity().getPackageManager()) != null) {
                startActivityForResult(pickIntent, REQUEST_PICK_IMAGE);
            } else {
                Intent getIntent = new Intent(Intent.ACTION_GET_CONTENT);
                getIntent.setType("image/*");
                getIntent.addCategory(Intent.CATEGORY_OPENABLE);
                startActivityForResult(Intent.createChooser(getIntent, "Choose Profile Photo"), REQUEST_PICK_IMAGE);
            }
        } catch (Exception ex) {
            sendError("Unable to open gallery: " + ex.getMessage());
            cleanupSelf();
        }
    }

    @Override
    public void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST_PICK_IMAGE) {
            return;
        }

        try {
            if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
                return;
            }

            Uri uri = data.getData();
            byte[] jpegBytes = loadCenterCroppedThumbnail(uri);
            if (jpegBytes == null || jpegBytes.length == 0) {
                sendError("Could not read selected image.");
                return;
            }

            String encoded = Base64.encodeToString(jpegBytes, Base64.NO_WRAP);
            if (targetGameObject != null && successMethod != null) {
                UnityPlayer.UnitySendMessage(targetGameObject, successMethod, encoded);
            }
        } catch (Exception ex) {
            sendError("Failed reading photo: " + ex.getMessage());
        } finally {
            cleanupSelf();
        }
    }

    private byte[] loadCenterCroppedThumbnail(Uri uri) throws Exception {
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        InputStream boundsStream = getActivity().getContentResolver().openInputStream(uri);
        if (boundsStream == null) return null;
        try {
            BitmapFactory.decodeStream(boundsStream, null, bounds);
        } finally {
            boundsStream.close();
        }

        int minDim = Math.min(bounds.outWidth, bounds.outHeight);
        int sampleSize = 1;
        while (minDim / (sampleSize * 2) >= TARGET_SIZE * 2) {
            sampleSize *= 2;
        }

        BitmapFactory.Options decodeOpts = new BitmapFactory.Options();
        decodeOpts.inSampleSize = sampleSize;
        Bitmap decoded;
        InputStream decodeStream = getActivity().getContentResolver().openInputStream(uri);
        if (decodeStream == null) return null;
        try {
            decoded = BitmapFactory.decodeStream(decodeStream, null, decodeOpts);
        } finally {
            decodeStream.close();
        }

        if (decoded == null) return null;

        int side = Math.min(decoded.getWidth(), decoded.getHeight());
        int startX = (decoded.getWidth() - side) / 2;
        int startY = (decoded.getHeight() - side) / 2;
        Bitmap cropped = Bitmap.createBitmap(decoded, startX, startY, side, side);
        Bitmap scaled = Bitmap.createScaledBitmap(cropped, TARGET_SIZE, TARGET_SIZE, true);

        if (cropped != decoded) cropped.recycle();
        decoded.recycle();

        try {
            int[] qualities = {80, 68, 56, 44, 32};
            byte[] last = null;
            for (int q : qualities) {
                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                scaled.compress(Bitmap.CompressFormat.JPEG, q, baos);
                last = baos.toByteArray();
                if (last.length <= MAX_BYTES) {
                    return last;
                }
            }
            return last;
        } finally {
            scaled.recycle();
        }
    }

    private void sendError(String message) {
        if (targetGameObject != null && errorMethod != null) {
            UnityPlayer.UnitySendMessage(targetGameObject, errorMethod, message);
        }
    }

    private void cleanupSelf() {
        try {
            Activity act = getActivity();
            if (act != null && !act.isFinishing()) {
                act.getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
            }
        } catch (Exception ignored) {
        }
    }
}
