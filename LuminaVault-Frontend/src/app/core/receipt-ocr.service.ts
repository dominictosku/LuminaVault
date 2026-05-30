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

/// Default implementation: lazy-wraps tesseract.js. The library is dynamic-imported on
/// first call to keep the initial bundle slim. The worker script, WASM core and English
/// language pack are all self-hosted under `/tesseract/` (copied from node_modules at build
/// time + a committed `eng.traineddata.gz`) rather than pulled from a CDN, so the SPA's CSP
/// needs no external origins. Paths must be absolute because the worker runs from a blob URL
/// and resolves `importScripts`/fetch against the document origin, not the blob.
@Injectable()
export class TesseractReceiptOcr extends ReceiptOcr {
  async recognize(file: File, onProgress?: (fraction: number) => void): Promise<string> {
    const { createWorker, OEM } = await import('tesseract.js');
    const base = `${location.origin}/tesseract`;
    const worker = await createWorker('eng', OEM.DEFAULT, {
      workerPath: `${base}/worker.min.js`,
      corePath: `${base}/core`,
      langPath: `${base}/lang`,
      logger: msg => {
        if (onProgress && msg.status === 'recognizing text') {
          onProgress(msg.progress ?? 0);
        }
      },
    });
    try {
      const result = await worker.recognize(file);
      return result.data.text ?? '';
    } finally {
      await worker.terminate();
    }
  }
}
