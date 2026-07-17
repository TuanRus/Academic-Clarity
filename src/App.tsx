import { BrowserRouter } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { BookmarkProvider } from './context/BookmarkContext';
import AppRoutes from './routes/AppRoutes';

const App = () => {
  return (
    <BrowserRouter>
      <AuthProvider>
        <BookmarkProvider>
          <AppRoutes />
        </BookmarkProvider>
      </AuthProvider>
    </BrowserRouter>
  );
};

export default App;
