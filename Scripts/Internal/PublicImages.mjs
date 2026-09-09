// Exact public-image allowlist. AuditPublicRelease verifies pixels against manual reviews.
export const publicImages = [
  ['docs/images/monitor-overview.png',1082,552,'docs/images/privacy-review.md'],
  ['docs/store-assets/screenshot-monitor-1280x800.png',1280,800,'docs/store-assets/privacy-review.md'],
  ['docs/store-assets/promo-small-440x280.png',440,280,'docs/store-assets/privacy-review.md'],
  ...[16,32,48,128].map(size => [`src/LazyChromeWindowBridge.Extension/icons/icon-${size}.png`,size,size,'docs/store-assets/privacy-review.md'])
];
