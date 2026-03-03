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

#ifdef __cplusplus
}
#endif
