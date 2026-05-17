import { Injectable } from '@angular/core';

/// Indirection over the OCR engine so page components don't import a vendor SDK
/// directly. `tesseract.js` is the default (entirely client-side, no server round
/// trip), but a `CloudReceiptOcr` adapter (Azure Vision, Google Vision, etc.) can
/// be swapped in via the DI registration in `app.config.ts` if local accuracy
/// becomes a limitation. Tests can supply a stub by providing `ReceiptOcr` with
/// `useValue: { recognize: () => Promise.resolve('hand-crafted text') }`.
export abstract class ReceiptOcr {
  abstract recognize(file: File, onProgress?: (fraction: number) => void): Promise<string>;
}

/// Default implementation: lazy-wraps tesseract.js. The library + English language
/// pack weighs ~3 MB, so we dynamic-import on first call to keep the initial bundle
/// slim. Subsequent calls reuse the cached module via the bundler's import cache.
@Injectable()
export class TesseractReceiptOcr extends ReceiptOcr {
  async recognize(file: File, onProgress?: (fraction: number) => void): Promise<string> {
    const { recognize } = await import('tesseract.js');
    const result = await recognize(file, 'eng', {
      logger: msg => {
        if (onProgress && msg.status === 'recognizing text') {
          onProgress(msg.progress ?? 0);
        }
      },
    });
    return result.data.text ?? '';
  }
}
