import { useEffect, useRef, useState } from 'react';
import { compileLatex } from '../../lib/api/latex';
import { ApiError } from '../../lib/http';

// Tab "PDF Preview": compile qua backend (POST /api/latex/compile) — backend proxy tới
// dịch vụ công cộng texlive.net (pdfLaTeX đầy đủ package). Không dùng WASM trong browser
// vì CDN texlive của SwiftLaTeX đã ngừng hoạt động; cần internet khi compile.

/** Giải mã base64 → blob URL cho iframe PDF. */
function base64ToPdfUrl(b64: string): string {
  const bin = atob(b64);
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
}

interface PdfPreviewProps {
  /** Lấy nội dung LaTeX hiện tại của editor tại thời điểm bấm Compile. */
  getSource: () => string;
  /** Parent tăng số này để yêu cầu compile (vd Ctrl+S). */
  compileSignal?: number;
}

const PdfPreview = ({ getSource, compileSignal }: PdfPreviewProps) => {
  const [compiling, setCompiling] = useState(false);
  const [pdfUrl, setPdfUrl] = useState<string | null>(null);
  const [log, setLog] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [copiedLog, setCopiedLog] = useState(false);
  const compiledOnceRef = useRef(false);

  // Thu hồi blob URL cũ khi unmount / thay PDF mới.
  useEffect(() => {
    return () => {
      if (pdfUrl) URL.revokeObjectURL(pdfUrl);
    };
  }, [pdfUrl]);

  const compile = () => {
    if (compiling) return;
    setCompiling(true);
    setError(null);
    setLog(null);
    compileLatex(getSource())
      .then((r) => {
        if (r.pdf) {
          setPdfUrl(base64ToPdfUrl(r.pdf)); // effect cleanup thu hồi URL cũ
        } else {
          setError('Compilation failed — see the TeX log below.');
          setLog(r.log || '(no log)');
        }
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'Could not reach the compile service.'))
      .finally(() => setCompiling(false));
  };

  // Tự compile lần đầu khi mở tab.
  useEffect(() => {
    if (!compiledOnceRef.current) {
      compiledOnceRef.current = true;
      compile();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Ctrl+S từ editor: chỉ compile khi signal TĂNG sau khi mount (mount đã auto-compile ở trên).
  const lastSignalRef = useRef(compileSignal ?? 0);
  useEffect(() => {
    if (compileSignal !== undefined && compileSignal !== lastSignalRef.current) {
      lastSignalRef.current = compileSignal;
      compile();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [compileSignal]);

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-center justify-between gap-2 border-b border-gray-100 px-4 py-2">
        <span className="text-xs text-gray-500">
          pdfLaTeX via texlive.net — full package support, needs internet. Limit: 1 compile / 10s.
        </span>
        <div className="flex shrink-0 items-center gap-2">
          {pdfUrl && !error && (
            <button
              type="button"
              onClick={() => window.open(pdfUrl, '_blank')}
              className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
            >
              Open in tab ↗
            </button>
          )}
          <button
            type="button"
            onClick={compile}
            disabled={compiling}
            className="rounded-md bg-indigo-700 px-3 py-1.5 text-xs font-medium text-white hover:bg-indigo-800 disabled:cursor-not-allowed disabled:opacity-40"
          >
            {compiling ? 'Compiling…' : 'Compile PDF'}
          </button>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        {!pdfUrl && !error && (
          <div className="flex h-full items-center justify-center p-4 text-center text-sm text-gray-400">
            {compiling ? 'Compiling your document…' : 'Click Compile PDF to render your document.'}
          </div>
        )}

        {error && (
          <div className="space-y-2 p-4">
            <div className="flex items-center justify-between gap-2">
              <p className="text-sm text-red-600">{error}</p>
              {log && (
                <button
                  type="button"
                  onClick={() => {
                    navigator.clipboard.writeText(log).then(() => {
                      setCopiedLog(true);
                      window.setTimeout(() => setCopiedLog(false), 2000);
                    });
                  }}
                  className="shrink-0 rounded-md border border-gray-300 px-2.5 py-1 text-xs font-medium text-gray-700 hover:bg-gray-50"
                >
                  {copiedLog ? '✓ Copied' : 'Copy log'}
                </button>
              )}
            </div>
            {log && (
              <pre className="max-h-[50vh] overflow-auto rounded-lg bg-gray-900 p-3 text-[11px] leading-relaxed text-gray-100">
                {log}
              </pre>
            )}
          </div>
        )}

        {!error && pdfUrl && <iframe title="PDF preview" src={pdfUrl} className="h-full w-full" />}
      </div>
    </div>
  );
};

export default PdfPreview;
