import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import RequireFeature from '../../routes/RequireFeature';
import { useFeature } from '../../hooks/useFeature';
import { FeaturePermission } from '../../types/permissions';
import MindMapGraph from '../../components/MindMapGraph';
import { getKeywordTree, getTopPapersByKeyword, suggestKeywords } from '../../lib/api/mindmap';
import { ApiError } from '../../lib/http';
import type { MindmapGraph, MindmapNode, PaperSearchItem } from '../../types/api';

// Research Landscape · Topic Network Graph - FR-11..FR-15
// FR-11..14 (render graph, size/color, zoom/pan) -> GRAPH_BASIC (mọi user).
// FR-15 (side panel chi tiết khi click node) -> GRAPH_ADVANCED (PREMIUM/ADMIN).
// ĐÃ NỐI API thật: GET /api/mindmap/tree/keyword + GET /api/mindmap/papers/keyword.
const ResearchLandscapePage = () => {
  const canSeeDetails = useFeature(FeaturePermission.GRAPH_ADVANCED);

  const [keyword, setKeyword] = useState('');
  const [input, setInput] = useState('');
  const [suggestions, setSuggestions] = useState<string[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);
  const [graph, setGraph] = useState<MindmapGraph | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Side panel (GRAPH_ADVANCED): top bài báo của node được click.
  const [selected, setSelected] = useState<MindmapNode | null>(null);
  const [papers, setPapers] = useState<PaperSearchItem[]>([]);
  const [papersLoading, setPapersLoading] = useState(false);

  useEffect(() => {
    if (!keyword) return;
    setLoading(true);
    setError(null);
    setSelected(null);
    setPapers([]);
    getKeywordTree(keyword, 6, 3)
      .then(setGraph)
      .catch((e) => {
        setGraph(null);
        setError(e instanceof ApiError ? e.message : 'Could not load the mind map.');
      })
      .finally(() => setLoading(false));
  }, [keyword]);

  // Autocomplete: gõ >= 2 ký tự -> gợi ý keyword (backend trả theo thứ tự nhiều bài -> ít).
  useEffect(() => {
    const term = input.trim();
    if (term.length < 2) {
      setSuggestions([]);
      return;
    }
    const id = setTimeout(() => {
      suggestKeywords(term, 10)
        .then(setSuggestions)
        .catch(() => setSuggestions([]));
    }, 250);
    return () => clearTimeout(id);
  }, [input]);

  const draw = (kw: string) => {
    const term = kw.trim();
    if (!term) return;
    setInput(term);
    setKeyword(term);
    setShowSuggestions(false);
  };

  const handleNodeClick = (node: MindmapNode) => {
    if (!canSeeDetails) return; // FR-15: chỉ Premium/Admin xem panel chi tiết
    setSelected(node);
    setPapersLoading(true);
    const distinctFrom = node.level && node.level > 0 ? keyword : undefined;
    getTopPapersByKeyword(node.label, distinctFrom, 8)
      .then(setPapers)
      .catch(() => setPapers([]))
      .finally(() => setPapersLoading(false));
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-medium uppercase tracking-wide text-gray-400">
            Keyword Network Graph
          </p>
          <h1 className="text-2xl font-bold text-gray-900">Research Keyword Landscape</h1>
        </div>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            draw(input);
          }}
          className="flex items-center gap-2"
        >
          <div className="relative">
            <input
              value={input}
              onChange={(e) => {
                setInput(e.target.value);
                setShowSuggestions(true);
              }}
              onFocus={() => setShowSuggestions(true)}
              onBlur={() => setTimeout(() => setShowSuggestions(false), 120)}
              placeholder="Type a keyword…"
              className="w-64 rounded-md border border-gray-200 px-3 py-1.5 text-sm"
            />
            {/* Gợi ý keyword: backend sắp xếp nhiều bài -> ít (top keyword to last) */}
            {showSuggestions && suggestions.length > 0 && (
              <ul className="absolute z-20 mt-1 max-h-72 w-64 overflow-y-auto rounded-md border border-gray-200 bg-white shadow-lg">
                {suggestions.map((s) => (
                  <li key={s}>
                    <button
                      type="button"
                      onMouseDown={(e) => e.preventDefault()}
                      onClick={() => draw(s)}
                      className="block w-full px-3 py-2 text-left text-sm text-gray-700 hover:bg-indigo-50"
                    >
                      {s}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <button
            type="submit"
            className="rounded-md bg-indigo-700 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-800"
          >
            Draw
          </button>
        </form>
      </div>

      {/* FR-11..14: GRAPH_BASIC - luôn hiển thị */}
      <RequireFeature feature={FeaturePermission.GRAPH_BASIC} featureLabel="Keyword Network Graph">
        <div className="grid gap-4 lg:grid-cols-[1fr_22rem]">
          <div>
            {loading && (
              <div className="flex h-[72vh] min-h-[520px] items-center justify-center rounded-xl border border-gray-200 bg-white text-sm text-gray-400">
                Building mind map…
              </div>
            )}
            {error && (
              <div className="flex h-[72vh] min-h-[520px] items-center justify-center rounded-xl border border-gray-200 bg-white text-sm text-red-600">
                {error}
              </div>
            )}
            {!loading && !error && !graph && (
              <div className="flex h-[72vh] min-h-[520px] items-center justify-center rounded-xl border border-dashed border-gray-300 bg-white px-6 text-center text-sm text-gray-400">
                Type a keyword above and pick a suggestion to build the keyword mind map.
              </div>
            )}
            {!loading && !error && graph && (
              <MindMapGraph graph={graph} onNodeClick={handleNodeClick} />
            )}
            {graph && (
              <p className="mt-2 text-xs text-gray-500">
                {graph.totalNodes} keywords · {graph.totalEdges} branches. Scroll to zoom, drag to pan.
                {canSeeDetails ? ' Click a node to see representative papers.' : ' Upgrade to Premium to see details on click.'}
              </p>
            )}
          </div>

          {/* Side panel chi tiết (FR-15) */}
          <div className="h-[72vh] min-h-[520px] overflow-y-auto rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            {!canSeeDetails ? (
              <p className="text-sm text-gray-400">
                The detail panel (top papers of a keyword) is available for Premium/Admin only.
              </p>
            ) : !selected ? (
              <p className="text-sm text-gray-400">Select a node on the mind map to see representative papers.</p>
            ) : (
              <>
                <p className="text-xs uppercase tracking-wide text-gray-400">Keyword</p>
                <h2 className="mb-3 text-base font-semibold text-gray-900">{selected.label}</h2>
                {papersLoading ? (
                  <p className="text-sm text-gray-400">Loading papers…</p>
                ) : papers.length === 0 ? (
                  <p className="text-sm text-gray-400">No papers for this keyword.</p>
                ) : (
                  <ul className="divide-y divide-gray-100">
                    {papers.map((p) => (
                      <li key={p.paperId} className="py-2">
                        <Link
                          to={`/papers/${encodeURIComponent(p.paperId)}`}
                          className="text-sm font-medium text-indigo-700 hover:underline"
                        >
                          {p.title}
                        </Link>
                        <p className="mt-0.5 text-xs text-gray-500">
                          {p.year ?? '—'} · {p.citationCount} citations
                        </p>
                      </li>
                    ))}
                  </ul>
                )}
              </>
            )}
          </div>
        </div>
      </RequireFeature>
    </div>
  );
};

export default ResearchLandscapePage;
