import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import CodeMirror from '@uiw/react-codemirror';
import { StreamLanguage } from '@codemirror/language';
import { stex } from '@codemirror/legacy-modes/mode/stex';
import { EditorView } from '@codemirror/view';
import RequireFeature from '../../routes/RequireFeature';
import OverlapPanel from '../../components/latex/OverlapPanel';
import PdfPreview from '../../components/latex/PdfPreview';
import { FeaturePermission } from '../../types/permissions';
import { getDoc, updateDoc, exportDoc } from '../../lib/latexStorage';
import type { Citation } from '../../types/api';

type RightTab = 'pdf' | 'overlap';

const tabClass = (active: boolean) =>
  active
    ? 'border-b-2 border-indigo-700 px-3 py-2 text-sm font-semibold text-indigo-700'
    : 'px-3 py-2 text-sm text-gray-500 hover:text-indigo-700';

const LatexEditorPage = () => {
  const { docId = '' } = useParams();
  const initialDoc = useMemo(() => getDoc(docId), [docId]);

  const [title, setTitle] = useState(initialDoc?.title ?? '');
  const [content, setContent] = useState(initialDoc?.content ?? '');
  const [saveState, setSaveState] = useState<'saved' | 'saving' | 'error'>('saved');
  const [saveError, setSaveError] = useState<string | null>(null);
  const [tab, setTab] = useState<RightTab>('pdf');
  // Tăng số này = yêu cầu PdfPreview compile (dùng cho Ctrl+S).
  const [compileSignal, setCompileSignal] = useState(0);

  const viewRef = useRef<EditorView | null>(null);
  const saveTimer = useRef<number | undefined>(undefined);
  // Ref giữ giá trị mới nhất cho autosave debounce (tránh closure cũ).
  const latest = useRef({ title, content });
  latest.current = { title, content };

  const extensions = useMemo(() => [StreamLanguage.define(stex), EditorView.lineWrapping], []);

  // Autosave: gõ xong 1s không đụng phím → ghi localStorage.
  // localStorage đầy → writeAll ném lỗi: hiện trạng thái lỗi thay vì "Saved" giả.
  const scheduleSave = () => {
    setSaveState('saving');
    window.clearTimeout(saveTimer.current);
    saveTimer.current = window.setTimeout(() => {
      try {
        updateDoc(docId, { title: latest.current.title, content: latest.current.content });
        setSaveState('saved');
        setSaveError(null);
      } catch (e) {
        setSaveState('error');
        setSaveError(e instanceof Error ? e.message : 'Could not save to browser storage.');
      }
    }, 1000);
  };

  // Rời trang giữa chừng → flush bản chưa ghi.
  useEffect(() => {
    return () => {
      window.clearTimeout(saveTimer.current);
      try {
        if (getDoc(docId)) updateDoc(docId, { title: latest.current.title, content: latest.current.content });
      } catch {
        // storage đầy: không chặn được unmount, dữ liệu vẫn còn trong phiên trước đó
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [docId]);

  // Đóng tab/reload khi chưa lưu xong (đang debounce hoặc lưu lỗi) → hỏi xác nhận.
  useEffect(() => {
    if (saveState === 'saved') return;
    const warn = (e: BeforeUnloadEvent) => {
      e.preventDefault();
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [saveState]);

  /** Chèn \cite tại cursor + \bibitem vào thebibliography (tạo env nếu chưa có, không trùng key). */
  const insertCitation = (c: Citation) => {
    const view = viewRef.current;
    if (!view) return;
    const doc = view.state.doc.toString();
    const changes: { from: number; insert: string }[] = [
      { from: view.state.selection.main.head, insert: `\\cite{${c.bibtexKey}}` },
    ];
    if (!doc.includes(`\\bibitem{${c.bibtexKey}}`)) {
      const endBib = doc.indexOf('\\end{thebibliography}');
      if (endBib >= 0) {
        changes.push({ from: endBib, insert: `${c.bibitem}\n` });
      } else {
        const env = `\n\\begin{thebibliography}{99}\n${c.bibitem}\n\\end{thebibliography}\n`;
        const endDoc = doc.indexOf('\\end{document}');
        changes.push({ from: endDoc >= 0 ? endDoc : doc.length, insert: env });
      }
    }
    view.dispatch({ changes }); // onChange của CodeMirror sẽ sync state + autosave
    view.focus();
  };

  // Ctrl+S / Cmd+S = compile PDF (thay vì dialog save của browser) — thói quen Overleaf.
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault();
        setTab('pdf');
        setCompileSignal((s) => s + 1);
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  if (!initialDoc) {
    return (
      <div className="rounded-xl border border-dashed border-gray-300 p-10 text-center">
        <p className="text-sm text-gray-400">Document not found — it may have been deleted from this browser.</p>
        <Link to="/latex" className="mt-2 inline-block text-sm font-medium text-indigo-700 hover:underline">
          ← Back to LaTeX Writer
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {/* Thanh tiêu đề: back + title editable + trạng thái lưu + export */}
      <div className="flex flex-wrap items-center gap-3">
        <Link to="/latex" className="text-sm text-gray-500 hover:text-indigo-700">
          ← All documents
        </Link>
        <input
          value={title}
          onChange={(e) => {
            setTitle(e.target.value);
            scheduleSave();
          }}
          placeholder="Untitled document"
          className="min-w-0 flex-1 rounded-md border border-transparent px-2 py-1 text-lg font-semibold text-gray-900 hover:border-gray-200 focus:border-gray-300 focus:outline-none"
        />
        <span
          className={`text-xs ${saveState === 'error' ? 'font-medium text-red-600' : 'text-gray-400'}`}
          title={saveError ?? undefined}
        >
          {saveState === 'saving' ? 'Saving…' : saveState === 'error' ? `Not saved — ${saveError}` : 'Saved ✓'}
        </span>
        <button
          type="button"
          onClick={() => exportDoc({ title: latest.current.title, content: latest.current.content })}
          className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
        >
          Export .tex
        </button>
      </div>

      <RequireFeature
        feature={FeaturePermission.LATEX_WRITER}
        featureLabel="LaTeX Writer"
        description="Soạn bài nghiên cứu bằng LaTeX với PDF preview, check trùng ý tưởng và chèn citation tự động — dành cho tài khoản Premium."
      >
        <div className="grid gap-4 lg:grid-cols-2">
          {/* LEFT: LaTeX source editor */}
          <div className="flex flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="border-b border-gray-100 px-4 py-2">
              <span className="text-sm font-semibold text-gray-700">LaTeX source</span>
            </div>
            <CodeMirror
              value={content}
              height="68vh"
              extensions={extensions}
              onCreateEditor={(view) => {
                viewRef.current = view;
              }}
              onChange={(value) => {
                setContent(value);
                scheduleSave();
              }}
              basicSetup={{ lineNumbers: true, foldGutter: false, highlightActiveLine: true }}
            />
          </div>

          {/* RIGHT: PDF preview / Overlap check */}
          <div className="flex min-h-0 flex-col overflow-hidden rounded-xl border border-gray-200 bg-white shadow-sm">
            <div className="flex border-b border-gray-100 px-2">
              <button type="button" className={tabClass(tab === 'pdf')} onClick={() => setTab('pdf')}>
                PDF Preview
              </button>
              <button type="button" className={tabClass(tab === 'overlap')} onClick={() => setTab('overlap')}>
                Overlap Check
              </button>
            </div>
            <div className="h-[64vh] min-h-[300px]">
              {tab === 'pdf' ? (
                <PdfPreview getSource={() => latest.current.content} compileSignal={compileSignal} />
              ) : (
                <OverlapPanel
                  getDraft={() => {
                    const view = viewRef.current;
                    const sel = view
                      ? view.state.sliceDoc(view.state.selection.main.from, view.state.selection.main.to)
                      : '';
                    return { full: latest.current.content, selection: sel };
                  }}
                  onCite={insertCitation}
                />
              )}
            </div>
          </div>
        </div>
      </RequireFeature>
    </div>
  );
};

export default LatexEditorPage;
