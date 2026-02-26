import './App.css'
import { useAuth } from './hooks/useAuth'
import { LoginPage } from './components/LoginPage'
import { AccessDeniedPage } from './components/AccessDeniedPage'
import { Layout } from './components/Layout'

function App() {
  const { user, isAuthenticated, isLoading, isAdmin, isRegistered, login, logout } = useAuth()

  if (isLoading) {
    return <div className="app-loading">Loading...</div>
  }

  if (!isAuthenticated || !user) {
    return <LoginPage login={login} />
  }

  if (!isRegistered) {
    return <AccessDeniedPage userDetails={user.userDetails} logout={logout} />
  }

  return <Layout user={user} isAdmin={isAdmin} logout={logout} />
}

export default App
