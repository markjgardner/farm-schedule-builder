import { useState } from 'react';
import { AvailabilityStatus } from '../types';
import { useAvailability } from '../hooks/useAvailability';

const STATUS_LABELS: Record<AvailabilityStatus, string> = {
  [AvailabilityStatus.Available]: 'Available',
  [AvailabilityStatus.NotAvailable]: 'Not Available',
  [AvailabilityStatus.MorningOnly]: 'Morning Only',
  [AvailabilityStatus.EveningOnly]: 'Evening Only',
};

const DAY_NAMES = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function formatDayLabel(dateStr: string): string {
  const [year, month, day] = dateStr.split('-').map(Number);
  const d = new Date(year, month - 1, day);
  const dayName = DAY_NAMES[d.getDay()];
  return `${dayName} ${month}/${day}`;
}

export function AvailabilityGrid() {
  const {
    availability,
    setDayAvailability,
    saveAll,
    isLoading,
    isSaving,
    error,
    windowStart,
    windowEnd,
    dates,
  } = useAvailability();

  const [toast, setToast] = useState<{
    message: string;
    type: 'success' | 'error';
  } | null>(null);

  const handleSave = async () => {
    const success = await saveAll();
    setToast(
      success
        ? { message: 'Availability saved!', type: 'success' }
        : { message: 'Failed to save. Please try again.', type: 'error' },
    );
    setTimeout(() => setToast(null), 3000);
  };

  if (isLoading) {
    return <div className="loading">Loading availability...</div>;
  }

  if (error) {
    return <div className="error-message">{error}</div>;
  }

  return (
    <div className="availability-grid">
      <h2>My Availability</h2>
      <p className="window-label">
        {windowStart} to {windowEnd}
      </p>
      <div className="grid-list">
        {dates.map((date) => (
          <div key={date} className="grid-row">
            <span className="day-label">{formatDayLabel(date)}</span>
            <select
              aria-label={`Availability for ${date}`}
              value={availability[date]}
              onChange={(e) =>
                setDayAvailability(date, e.target.value as AvailabilityStatus)
              }
            >
              {Object.entries(STATUS_LABELS).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </div>
        ))}
      </div>
      <button className="save-btn" onClick={handleSave} disabled={isSaving}>
        {isSaving ? 'Saving...' : 'Save Availability'}
      </button>
      {toast && (
        <div className={`toast toast-${toast.type}`} role="status">
          {toast.message}
        </div>
      )}
    </div>
  );
}
