// Template khởi tạo cho LaTeX Writer. Template Article có sẵn abstract + thebibliography
// để luồng "viết → check overlap → cite" hoạt động ngay không cần user tự dựng khung.

export interface LatexTemplate {
  id: string;
  name: string;
  description: string;
  content: string;
}

export const LATEX_TEMPLATES: LatexTemplate[] = [
  {
    id: 'article',
    name: 'Research Article',
    description: 'Khung bài báo chuẩn: abstract, sections, tài liệu tham khảo.',
    content: `\\documentclass[11pt]{article}
\\usepackage[margin=1in]{geometry}
\\usepackage{amsmath}
\\usepackage{graphicx}

\\title{Your Paper Title}
\\author{Your Name \\\\ Your Institution}
\\date{\\today}

\\begin{document}

\\maketitle

\\begin{abstract}
Write your abstract here (80--6000 characters). This text is what the
Overlap Check tab analyzes against the paper corpus, so summarize your
core idea, method, and expected contribution clearly.
\\end{abstract}

\\section{Introduction}
Introduce the problem and your motivation. Cite related work like this: \\cite{example2024}.

\\section{Methodology}
Describe your approach.

\\section{Results}
Present your findings.

\\section{Conclusion}
Summarize contributions and future work.

\\begin{thebibliography}{99}
\\bibitem{example2024} Example Author. An example reference entry. \\textit{Example Journal}, 2024.
\\end{thebibliography}

\\end{document}
`,
  },
  {
    id: 'ieee',
    name: 'IEEE-style (two column)',
    description: 'Bố cục 2 cột kiểu hội nghị IEEE (bản đơn giản, không cần IEEEtran).',
    content: `\\documentclass[10pt,twocolumn]{article}
\\usepackage[margin=0.75in]{geometry}
\\usepackage{amsmath}
\\usepackage{graphicx}

\\title{\\LARGE Your Conference Paper Title}
\\author{First Author \\\\ Institution \\\\ email@example.com}
\\date{}

\\begin{document}

\\maketitle

\\begin{abstract}
Write your abstract here. Keep it between 80 and 6000 characters so the
Overlap Check tab can analyze it against the corpus.
\\end{abstract}

\\section{Introduction}
Problem statement and contributions.

\\section{Related Work}
Position your work against existing research.

\\section{Proposed Method}
Your approach.

\\section{Experiments}
Setup and results.

\\section{Conclusion}
Summary and future directions.

\\begin{thebibliography}{99}
\\end{thebibliography}

\\end{document}
`,
  },
  {
    id: 'blank',
    name: 'Blank',
    description: 'Tài liệu trống tối thiểu.',
    content: `\\documentclass{article}
\\begin{document}

Hello, \\LaTeX!

\\end{document}
`,
  },
];
