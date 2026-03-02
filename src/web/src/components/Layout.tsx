import { useState } from 'react';
import type { ClientPrincipal } from '../types';
import { AdminPage } from './AdminPage';
import { AvailabilityGrid } from './AvailabilityGrid';

type Tab = 'availability' | 'admin';

interface LayoutProps {
  user: ClientPrincipal;
  isAdmin: boolean;
  isActive: boolean;
  logout: () => void;
}

export function Layout({ user, isAdmin, isActive, logout }: LayoutProps) {
  const [activeTab, setActiveTab] = useState<Tab>(!isActive && isAdmin ? 'admin' : 'availability');

  return (
    <div className="layout">
      <header className="app-header">
        <h1 className="app-title">🐴 Farm Schedule Builder</h1>
        <div className="user-info">
          <span className="user-name">{user.userDetails}</span>
          <button className="logout-btn" onClick={logout}>
            Logout
          </button>
        </div>
      </header>
      <nav className="tab-nav">
        {isActive && (
          <button
            className={`tab-btn ${activeTab === 'availability' ? 'active' : ''}`}
            onClick={() => setActiveTab('availability')}
          >
            My Availability
          </button>
        )}
        {isAdmin && (
          <button
            className={`tab-btn ${activeTab === 'admin' ? 'active' : ''}`}
            onClick={() => setActiveTab('admin')}
          >
            Admin
          </button>
        )}
      </nav>
      <main className="content">
        {activeTab === 'availability' && <AvailabilityGrid isAdmin={isAdmin} />}
        {activeTab === 'admin' && isAdmin && <AdminPage />}
      </main>
    </div>
  );
}
