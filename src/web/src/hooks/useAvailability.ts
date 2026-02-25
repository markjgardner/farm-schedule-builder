import { useCallback, useEffect, useMemo, useState } from 'react';
import { AvailabilityStatus } from '../types';
import { getAvailability, saveAvailability } from '../services/api';

export function formatDate(d: Date): string {
  return d.toISOString().split('T')[0];
}

export function getNextMonday(): Date {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const day = today.getDay();
  const daysUntilMonday = day === 0 ? 1 : 8 - day;
  const nextMonday = new Date(today);
  nextMonday.setDate(today.getDate() + daysUntilMonday);
  return nextMonday;
}

function buildDates(start: Date): string[] {
  const dates: string[] = [];
  for (let i = 0; i < 14; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    dates.push(formatDate(d));
  }
  return dates;
}

interface UseAvailabilityResult {
  availability: Record<string, AvailabilityStatus>;
  setDayAvailability: (date: string, status: AvailabilityStatus) => void;
  saveAll: () => Promise<boolean>;
  isLoading: boolean;
  isSaving: boolean;
  error: string | null;
  windowStart: string;
  windowEnd: string;
  dates: string[];
}

export function useAvailability(): UseAvailabilityResult {
  const windowStartDate = useMemo(() => getNextMonday(), []);
  const dates = useMemo(() => buildDates(windowStartDate), [windowStartDate]);
  const windowStart = formatDate(windowStartDate);
  const windowEndDate = new Date(windowStartDate);
  windowEndDate.setDate(windowStartDate.getDate() + 13);
  const windowEnd = formatDate(windowEndDate);

  const [availability, setAvailability] = useState<
    Record<string, AvailabilityStatus>
  >(() => {
    const init: Record<string, AvailabilityStatus> = {};
    for (const d of dates) {
      init[d] = AvailabilityStatus.Available;
    }
    return init;
  });

  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getAvailability(windowStart)
      .then((items) => {
        if (items.length > 0) {
          setAvailability((prev) => {
            const updated = { ...prev };
            for (const item of items) {
              updated[item.date] = item.status as AvailabilityStatus;
            }
            return updated;
          });
        }
      })
      .catch(() => {
        setError('Failed to load availability.');
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [windowStart]);

  const setDayAvailability = useCallback(
    (date: string, status: AvailabilityStatus) => {
      setAvailability((prev) => ({ ...prev, [date]: status }));
    },
    [],
  );

  const saveAll = useCallback(async (): Promise<boolean> => {
    setIsSaving(true);
    try {
      const items = dates.map((date) => ({
        date,
        status: availability[date],
      }));
      await saveAvailability(windowStart, items);
      return true;
    } catch {
      return false;
    } finally {
      setIsSaving(false);
    }
  }, [dates, availability, windowStart]);

  return {
    availability,
    setDayAvailability,
    saveAll,
    isLoading,
    isSaving,
    error,
    windowStart,
    windowEnd,
    dates,
  };
}
