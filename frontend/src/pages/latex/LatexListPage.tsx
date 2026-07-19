import { useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import RequireFeature from '../../routes/RequireFeature';
import { FeaturePermission } from '../../types/permissions';
import { LATEX_TEMPLATES } from '../../lib/latexTemplates';
import {
  listDocs,
  createDoc,
  deleteDoc,
  getDoc,
  exportDoc,
  importDocFromFile,
  type LatexDocMeta,
} from '../../lib/latexStorage';

const formatTime = (iso: string) =>
  new Date(iso).toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });

const LatexListPage = () => {
  const navigate = useNavigate();
  const [docs, setDocs] = useState<LatexDocMeta[]>(() => listDocs());
  const [showTemplates, setShowTemplates] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const refresh = () => setDocs(listDocs());

  const handleCreate = (templateId: string) => {
    const tpl = LATEX_TEMPLATES.find((t) => t.id === templateId);
    if (!tpl) return;
    const doc = createDoc('Untitled document', tpl.content);
    navigate(`/latex/${doc.id}`);
  };

  const handleDelete = (meta: LatexDocMeta) => {
    if (!window.confirm(`Delete "${meta.title}"? This cannot be undone.`)) return;
    deleteDoc(meta.id);
    refresh();
  };

  const handleExport = (id: string) => {
    const doc = getDoc(id);
    if (doc) exportDoc(doc);
  };

  const handleImport = (file: File | null) => {
    if (!file) return;
    importDocFromFile(file).then((doc) => navigate(`/latex/${doc.id}`));
  };

  return (
    <div className="space-y-5">
      <div>
        <p className="text-xs font-medium uppercase tracking-wide text-gray-400">Premium · Research Assistant</p>
        <h1 className="text-2xl font-bold text-gray-900">LaTeX Writer</h1>
        <p className="mt-1 max-w-3xl text-sm text-gray-500">
          Write your research paper in LaTeX right here — compile a PDF preview in the browser, run the{' '}
          <strong className="text-gray-700">Idea Overlap Checker</strong> on your draft's abstract, and insert
          citations from matching papers in one click.
        </p>
      </div>

      <RequireFeature
        feature={FeaturePermission.LATEX_WRITER}
        featureLabel="LaTeX Writer"
        description="Soạn bài nghiên cứu bằng LaTeX với PDF preview, check trùng ý tưởng và chèn citation tự động — dành cho tài khoản Premium."
      >
        {/* Tài liệu lưu localStorage — nhắc user backup bằng Export */}
        <div className="rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
          Documents are stored <strong>in this browser only</strong> — use <strong>Export .tex</strong> to back
          them up or continue on another machine.
        </div>

        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => setShowTemplates((v) => !v)}
            className="rounded-md bg-indigo-700 px-4 py-2 text-sm font-medium text-white hover:bg-indigo-800"
          >
            + New document
          </button>
          <button
            type="button"
            onClick={() => fileInputRef.current?.click()}
            className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Import .tex
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".tex,text/x-tex"
            className="hidden"
            onChange={(e) => {
              handleImport(e.target.files?.[0] ?? null);
              e.target.value = '';
            }}
          />
        </div>

        {/* Chọn template khi tạo mới */}
        {showTemplates && (
          <div className="grid gap-3 sm:grid-cols-3">
            {LATEX_TEMPLATES.map((tpl) => (
              <button
                key={tpl.id}
                type="button"
                onClick={() => handleCreate(tpl.id)}
                className="rounded-xl border border-gray-200 bg-white p-4 text-left shadow-sm hover:border-indigo-300 hover:shadow"
              >
                <p className="text-sm font-semibold text-gray-800">{tpl.name}</p>
                <p className="mt-0.5 text-xs text-gray-500">{tpl.description}</p>
              </button>
            ))}
          </div>
        )}

        {/* Danh sách tài liệu */}
        {docs.length === 0 ? (
          <div className="rounded-xl border border-dashed border-gray-300 p-10 text-center">
            <p className="text-sm text-gray-400">
              No documents yet. Create one from a template or import a <code>.tex</code> file.
            </p>
          </div>
        ) : (
          <ul className="space-y-3">
            {docs.map((d) => (
              <li
                key={d.id}
                className="flex items-center justify-between gap-4 rounded-xl border border-gray-200 bg-white p-4 shadow-sm"
              >
                <button
                  type="button"
                  onClick={() => navigate(`/latex/${d.id}`)}
                  className="min-w-0 flex-1 text-left"
                >
                  {/* Tài liệu bị xóa hết tên trong editor → hiển thị fallback thay vì dòng trống */}
                  <p className={`truncate text-sm font-semibold hover:underline ${d.title.trim() ? 'text-indigo-700' : 'italic text-gray-400'}`}>
                    {d.title.trim() || 'Untitled document'}
                  </p>
                  <p className="mt-0.5 text-xs text-gray-500">Last edited {formatTime(d.updatedAt)}</p>
                </button>
                <div className="flex shrink-0 items-center gap-2">
                  <button
                    type="button"
                    onClick={() => handleExport(d.id)}
                    className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50"
                  >
                    Export .tex
                  </button>
                  <button
                    type="button"
                    onClick={() => handleDelete(d)}
                    className="rounded-md border border-red-600 px-3 py-1.5 text-xs font-medium text-red-600 hover:bg-red-50"
                  >
                    Delete
                  </button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </RequireFeature>
    </div>
  );
};

export default LatexListPage;
