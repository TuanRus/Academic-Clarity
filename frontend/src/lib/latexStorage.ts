// Lưu trữ tài liệu LaTeX PHÍA CLIENT (localStorage) — quyết định thiết kế:
// backend KHÔNG có bảng LatexDocument để tránh xung đột schema/migration trong nhóm.
// Đánh đổi: dữ liệu gắn với trình duyệt này (mất khi xóa cache, không sync máy khác)
// → bù bằng Export/Import file .tex. Ghi nhận là known limitation trong SRS.

export interface LatexDoc {
  id: string;
  title: string;
  content: string;
  createdAt: string; // ISO
  updatedAt: string; // ISO
}

export type LatexDocMeta = Omit<LatexDoc, 'content'> & {
  /** Kích thước nội dung (byte, UTF-8) — cho user thấy tài liệu chiếm bao nhiêu localStorage. */
  sizeBytes: number;
};

const STORAGE_KEY = 'stt_latex_docs';

function readAll(): LatexDoc[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return []; // JSON hỏng → coi như chưa có tài liệu, không crash trang
  }
}

function writeAll(docs: LatexDoc[]): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(docs));
  } catch (e) {
    // localStorage đầy (QuotaExceededError) → nếu nuốt lỗi êm thì autosave "Saved" sẽ nói dối
    // và user mất bài. Ném tiếp để UI (autosave indicator) báo lỗi + khuyên Export .tex.
    throw new Error('Browser storage is full — export your documents as .tex and delete old ones to free space.');
  }
}

/** Danh sách metadata (không kèm content), mới sửa gần nhất lên đầu. */
export function listDocs(): LatexDocMeta[] {
  return readAll()
    .map(({ id, title, createdAt, updatedAt, content }) => ({
      id,
      title,
      createdAt,
      updatedAt,
      sizeBytes: new Blob([content]).size,
    }))
    .sort((a, b) => b.updatedAt.localeCompare(a.updatedAt));
}

/** Tổng dung lượng key lưu trữ đang chiếm (byte) — cảnh báo sớm trước khi localStorage đầy (~5MB). */
export function storageUsageBytes(): number {
  const raw = localStorage.getItem(STORAGE_KEY);
  return raw ? new Blob([raw]).size : 0;
}

/** "12.4 KB" / "1.2 MB" — hiển thị thân thiện. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

export function getDoc(id: string): LatexDoc | null {
  return readAll().find((d) => d.id === id) ?? null;
}

export function createDoc(title: string, content: string): LatexDoc {
  const now = new Date().toISOString();
  const doc: LatexDoc = {
    id: crypto.randomUUID(),
    title: title.trim() || 'Untitled document',
    content,
    createdAt: now,
    updatedAt: now,
  };
  writeAll([...readAll(), doc]);
  return doc;
}

export function updateDoc(id: string, patch: Partial<Pick<LatexDoc, 'title' | 'content'>>): LatexDoc | null {
  const docs = readAll();
  const idx = docs.findIndex((d) => d.id === id);
  if (idx < 0) return null;
  docs[idx] = { ...docs[idx], ...patch, updatedAt: new Date().toISOString() };
  writeAll(docs);
  return docs[idx];
}

export function deleteDoc(id: string): void {
  writeAll(readAll().filter((d) => d.id !== id));
}

/** Tải tài liệu về máy dưới dạng file .tex (backup / dùng với Overleaf thật). */
export function exportDoc(doc: Pick<LatexDoc, 'title' | 'content'>): void {
  const blob = new Blob([doc.content], { type: 'application/x-tex' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${doc.title.replace(/[\\/:*?"<>|]/g, '_') || 'document'}.tex`;
  a.click();
  URL.revokeObjectURL(url);
}

/** Đọc file .tex user chọn → tạo tài liệu mới. */
export function importDocFromFile(file: File): Promise<LatexDoc> {
  return file.text().then((content) => createDoc(file.name.replace(/\.tex$/i, ''), content));
}
