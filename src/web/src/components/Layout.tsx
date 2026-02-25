import { useState } from 'react';
import type { ClientPrincipal } from '../types';
import { AvailabilityGrid } from './AvailabilityGrid';
import { ScheduleView } from './ScheduleView';

type Tab = 'availability' | 'schedule';

interface LayoutProps {
  user: ClientPrincipal;
  logout: () => void;
}

export function Layout({ user, logout }: LayoutProps) {
  const [activeTab, setActiveTab] = useState<Tab>('availability');

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
        <button
          className={`tab-btn ${activeTab === 'availability' ? 'active' : ''}`}
          onClick={() => setActiveTab('availability')}
        >
          My Availability
        </button>
        <button
          className={`tab-btn ${activeTab === 'schedule' ? 'active' : ''}`}
          onClick={() => setActiveTab('schedule')}
        >
          Current Schedule
        </button>
      </nav>
      <main className="content">
        {activeTab === 'availability' ? <AvailabilityGrid /> : <ScheduleView />}
      </main>
    </div>
  );
}
