import { useEffect, useRef } from 'react';
import { DataSet } from 'vis-data';
import { Network, type Edge, type Node } from 'vis-network';
import type { MindmapGraph, MindmapNode } from '../types/api';

interface Props {
  graph: MindmapGraph;
  onNodeClick?: (node: MindmapNode) => void;
  height?: number | string;
}

// Màu đậm dần theo độ "hot" (trendScore) — giống trendColor() trong demo BE.
function trendColor(score?: number | null): string {
  if (score == null) return '#10b981';
  const g = Math.round(120 + score * 100);
  return `rgb(16, ${g}, ${Math.round(110 + (1 - score) * 30)})`;
}

/**
 * Mind map dạng CÂY phân tầng trái→phải bằng vis-network — render giống trang demo của backend.
 * Chỉ vẽ node keyword (chủ đề → con → cháu). Click 1 node -> onNodeClick(node).
 */
const MindMapGraph = ({ graph, onNodeClick, height = '72vh' }: Props) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const networkRef = useRef<Network | null>(null);
  // Giữ callback mới nhất mà không phải dựng lại network.
  const clickRef = useRef(onNodeClick);
  clickRef.current = onNodeClick;

  useEffect(() => {
    if (!containerRef.current) return;

    // Chỉ giữ node keyword (bài báo không nằm trên cây — click mới xổ danh sách).
    const kwNodes = graph.nodes.filter((n) => n.type === 'keyword');
    const kwIds = new Set(kwNodes.map((n) => n.id));
    const origById = new Map(kwNodes.map((n) => [n.id, n]));

    const nodes = new DataSet(
      kwNodes.map((n) => {
        const isCentral = (n.level ?? 0) === 0;
        return {
          id: n.id,
          label: n.label,
          level: n.level ?? 0,
          shape: 'dot',
          // Kẹp trần kích thước: node nhiều bài (vd "artificial intelligence" ~2000 bài) không phình quá lớn.
          // Dùng log để giãn nhẹ theo số bài + Math.min chặn trần ~34 (node tâm = 30).
          size: isCentral ? 30 : Math.min(34, 10 + Math.log2((n.paperCount || 1) + 1) * 3),
          color: isCentral
            ? { background: '#4338ca', border: '#312e81' }
            : { background: trendColor(n.trendScore), border: '#0d9488' },
          font: { color: '#1f2937', size: isCentral ? 18 : 14, strokeWidth: 4, strokeColor: '#ffffff' },
          title:
            `${isCentral ? 'TOPIC' : 'SUBTOPIC'}: ${n.label}\nPapers: ${n.paperCount ?? 0}` +
            (n.trendScore != null ? `\nTrendScore: ${n.trendScore}` : '') +
            '\nClick to see papers',
        };
      }) as Node[],
    );

    const edges = new DataSet(
      graph.edges
        .filter((e) => kwIds.has(e.source) && kwIds.has(e.target))
        .map((e, i) => ({
          id: i,
          from: e.source,
          to: e.target,
          color: { color: '#cbd5e1' },
        })) as Edge[],
    );

    const network = new Network(
      containerRef.current,
      { nodes, edges },
      {
        // Bố cục tỏa tròn "bông hoa": node tâm ở giữa, nhánh xòe ra bằng physics (force-directed).
        layout: { hierarchical: { enabled: false } },
        physics: {
          enabled: true,
          stabilization: { iterations: 200 },
          barnesHut: {
            gravitationalConstant: -12000,
            centralGravity: 0.3,
            springLength: 150,
            springConstant: 0.04,
            damping: 0.4,
          },
        },
        interaction: { hover: true, tooltipDelay: 100, navigationButtons: false },
        nodes: { borderWidth: 2 },
        edges: { smooth: { enabled: true, type: 'continuous', roundness: 0.5 } },
      },
    );

    network.on('click', (params: { nodes: string[] }) => {
      if (!params.nodes.length) return;
      const orig = origById.get(params.nodes[0]);
      if (orig) clickRef.current?.(orig);
    });

    networkRef.current = network;
    return () => {
      network.destroy();
      networkRef.current = null;
    };
  }, [graph]);

  return (
    <div
      ref={containerRef}
      style={{ height }}
      className="min-h-[520px] w-full rounded-xl border border-gray-200 bg-white"
    />
  );
};

export default MindMapGraph;
