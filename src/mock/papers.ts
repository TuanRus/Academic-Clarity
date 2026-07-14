import type { BookmarkedPaper } from '../context/BookmarkContext';

export interface PaperDetail extends BookmarkedPaper {
  abstract: string;
  doi: string;
  openAlexId: string;
  venue: string;
}

export const SAMPLE_PAPER: PaperDetail = {
  paperId: 'W2741809807',
  title: 'An Introduction to Deep Machine Learning Frameworks',
  authors: ['Ashish Vaswani', 'Noam Shazeer', 'Niki Parmar', 'Jakob Uszkoreit'],
  journal: 'Advances in Neural Information Processing Systems (NeurIPS)',
  venue: 'NeurIPS 2017',
  year: 2017,
  keywords: ['transformers', 'attention mechanism', 'neural networks', 'machine learning'],
  doi: '10.5555/3295222.3295349',
  openAlexId: 'W2741809807',
  abstract:
    'The dominant sequence transduction models are based on complex recurrent or convolutional neural networks that include an encoder and a decoder. We propose a new simple network architecture, the Transformer, based solely on attention mechanisms.',
};
