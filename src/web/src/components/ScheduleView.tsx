import { useEffect, useMemo, useState } from 'react';
import { Barn, ShiftTime } from '../types';
import type { Schedule } from '../types';
import { getCurrentSchedule } from '../services/api';
import { formatDate, getNextMonday } from '../hooks/useAvailability';

function getAssignment(
  schedule: Schedule,
  date: string,
  barn: Barn,
  shift: ShiftTime,
): string {
  const match = schedule.assignments.find(
    (a) => a.date === date && a.barn === barn && a.shift === shift,
  );
  return match ? match.workerName : 'UNFILLED';
}

function uniqueDates(schedule: Schedule): string[] {
  const set = new Set<string>();
  for (const a of schedule.assignments) {
    set.add(a.date);
  }
  return Array.from(set).sort();
}

export function ScheduleView() {
  const windowStart = useMemo(() => formatDate(getNextMonday()), []);
  const [schedule, setSchedule] = useState<Schedule | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getCurrentSchedule(windowStart)
      .then((data) => {
        setSchedule(data);
      })
      .catch(() => {
        setError('Failed to load schedule.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [windowStart]);

  if (isLoading) {
    return <div className="loading">Loading schedule...</div>;
  }

  if (error) {
    return <div className="error-message">{error}</div>;
  }

  if (!schedule || schedule.assignments.length === 0) {
    return (
      <div className="no-schedule">
        <h2>Current Schedule</h2>
        <p>No schedule available yet.</p>
      </div>
    );
  }

  const dates = uniqueDates(schedule);
  const columns: { barn: Barn; shift: ShiftTime; label: string }[] = [
    {
      barn: Barn.Windhover,
      shift: ShiftTime.Morning,
      label: 'Windhover Morning',
    },
    {
      barn: Barn.Windhover,
      shift: ShiftTime.Evening,
      label: 'Windhover Evening',
    },
    { barn: Barn.York, shift: ShiftTime.Morning, label: 'York Morning' },
    { barn: Barn.York, shift: ShiftTime.Evening, label: 'York Evening' },
  ];

  return (
    <div className="schedule-view">
      <h2>Current Schedule</h2>
      <p className="schedule-meta">
        Generated: {new Date(schedule.generatedAt).toLocaleDateString()}
      </p>
      <div className="table-wrapper">
        <table className="schedule-table">
          <thead>
            <tr>
              <th>Date</th>
              {columns.map((col) => (
                <th key={col.label}>{col.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {dates.map((date) => (
              <tr key={date}>
                <td>{date}</td>
                {columns.map((col) => {
                  const name = getAssignment(
                    schedule,
                    date,
                    col.barn,
                    col.shift,
                  );
                  return (
                    <td
                      key={col.label}
                      className={name === 'UNFILLED' ? 'unfilled' : ''}
                    >
                      {name}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
