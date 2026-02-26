import './App.css'
import { useAuth } from './hooks/useAuth'
import { LoginPage } from './components/LoginPage'
import { Layout } from './components/Layout'

function App() {
  const { user, isAuthenticated, isLoading, isAdmin, login, logout } = useAuth()

  if (isLoading) {
    return <div className="app-loading">Loading...</div>
  }

  if (!isAuthenticated || !user) {
    return <LoginPage login={login} />
  }

  return <Layout user={user} isAdmin={isAdmin} logout={logout} />
}

export default App
