#import <UIKit/UIKit.h>
#import <MessageUI/MessageUI.h>

// ──────────────────────────────────────
// MFMailComposeViewController delegate
// ──────────────────────────────────────
@interface DevianMailDelegate : NSObject <MFMailComposeViewControllerDelegate>
@end

@implementation DevianMailDelegate

- (void)mailComposeController:(MFMailComposeViewController *)controller
          didFinishWithResult:(MFMailComposeResult)result
                        error:(NSError *)error {
    [controller dismissViewControllerAnimated:YES completion:nil];
}

@end

// static 인스턴스: delegate 해제 방지
static DevianMailDelegate *_mailDelegate = nil;


// ──────────────────────────────────────
// UIDocumentPickerViewController delegate
// ──────────────────────────────────────
@interface DevianFilePickerDelegate : NSObject <UIDocumentPickerDelegate>
@end

@implementation DevianFilePickerDelegate

- (void)documentPicker:(UIDocumentPickerViewController *)controller
    didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    if (urls.count > 0) {
        NSString *filePath = urls.firstObject.path;
        UnitySendMessage("RecoveryManager", "OnFilePickerResult",
                         [filePath UTF8String]);
    } else {
        UnitySendMessage("RecoveryManager", "OnFilePickerResult", "");
    }
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller {
    UnitySendMessage("RecoveryManager", "OnFilePickerResult", "");
}

@end

static DevianFilePickerDelegate *_filePickerDelegate = nil;


#ifdef __cplusplus
extern "C" {
#endif

// ──────────────────────────────────────
// 범용 공유 시트 (UIActivityViewController)
// ──────────────────────────────────────
void DevianShare_ShareFile(const char* filePath, const char* subject) {
    if (filePath == NULL) return;

    NSString *path = [NSString stringWithUTF8String:filePath];
    NSURL *fileURL = [NSURL fileURLWithPath:path];

    if (![[NSFileManager defaultManager] fileExistsAtPath:path]) return;

    NSArray *items = @[fileURL];
    UIActivityViewController *avc =
        [[UIActivityViewController alloc] initWithActivityItems:items
                                          applicationActivities:nil];

    if (subject != NULL) {
        [avc setValue:[NSString stringWithUTF8String:subject] forKey:@"subject"];
    }

    UIViewController *rootVC =
        UnityGetGLViewController();

    // iPad: popoverPresentationController 필수
    if (avc.popoverPresentationController != nil) {
        avc.popoverPresentationController.sourceView = rootVC.view;
        avc.popoverPresentationController.sourceRect =
            CGRectMake(rootVC.view.bounds.size.width / 2, rootVC.view.bounds.size.height / 2, 0, 0);
        avc.popoverPresentationController.permittedArrowDirections = 0;
    }

    [rootVC presentViewController:avc animated:YES completion:nil];
}

// ──────────────────────────────────────
// 이메일 전용 (MFMailComposeViewController)
// ──────────────────────────────────────
void DevianShare_SendEmail(const char* filePath, const char* recipient, const char* subject) {
    if (filePath == NULL || recipient == NULL) return;
    if (![MFMailComposeViewController canSendMail]) return;

    NSString *path = [NSString stringWithUTF8String:filePath];
    if (![[NSFileManager defaultManager] fileExistsAtPath:path]) return;

    NSData *fileData = [NSData dataWithContentsOfFile:path];
    if (fileData == nil) return;

    NSString *fileName = [path lastPathComponent];
    NSString *recipientStr = [NSString stringWithUTF8String:recipient];

    MFMailComposeViewController *mailVC = [[MFMailComposeViewController alloc] init];

    if (_mailDelegate == nil) {
        _mailDelegate = [[DevianMailDelegate alloc] init];
    }
    mailVC.mailComposeDelegate = _mailDelegate;

    [mailVC setToRecipients:@[recipientStr]];

    if (subject != NULL) {
        [mailVC setSubject:[NSString stringWithUTF8String:subject]];
    }

    [mailVC addAttachmentData:fileData
                     mimeType:@"application/octet-stream"
                     fileName:fileName];

    UIViewController *rootVC = UnityGetGLViewController();
    [rootVC presentViewController:mailVC animated:YES completion:nil];
}

// ──────────────────────────────────────
// 파일 선택 다이얼로그 (UIDocumentPickerViewController)
// ──────────────────────────────────────
void DevianShare_PickFile() {
    if (_filePickerDelegate == nil) {
        _filePickerDelegate = [[DevianFilePickerDelegate alloc] init];
    }

    NSArray *documentTypes = @[@"public.data"];
    UIDocumentPickerViewController *picker =
        [[UIDocumentPickerViewController alloc] initWithDocumentTypes:documentTypes
                                                              inMode:UIDocumentPickerModeImport];
    picker.delegate = _filePickerDelegate;
    picker.allowsMultipleSelection = NO;

    if (@available(iOS 13.0, *)) {
        picker.shouldShowFileExtensions = YES;
    }

    // iOS 14+: Downloads 폴더를 초기 위치로 힌트
    if (@available(iOS 14.0, *)) {
        NSURL *downloadsURL = [[NSFileManager defaultManager]
            URLForDirectory:NSDownloadsDirectory
                   inDomain:NSUserDomainMask
          appropriateForURL:nil
                     create:NO
                      error:nil];
        if (downloadsURL != nil) {
            picker.directoryURL = downloadsURL;
        }
    }

    UIViewController *rootVC = UnityGetGLViewController();

    // iPad: popoverPresentationController 필수
    if (picker.popoverPresentationController != nil) {
        picker.popoverPresentationController.sourceView = rootVC.view;
        picker.popoverPresentationController.sourceRect =
            CGRectMake(rootVC.view.bounds.size.width / 2, rootVC.view.bounds.size.height / 2, 0, 0);
        picker.popoverPresentationController.permittedArrowDirections = 0;
    }

    [rootVC presentViewController:picker animated:YES completion:nil];
}

#ifdef __cplusplus
}
#endif
