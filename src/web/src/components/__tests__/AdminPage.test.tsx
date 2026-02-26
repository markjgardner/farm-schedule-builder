import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Worker } from '../../types';

const mockGetAdminWorkers = vi.fn();
const mockAddWorker = vi.fn();
const mockDeactivateWorker = vi.fn();
const mockActivateWorker = vi.fn();
const mockSetWorkerAdmin = vi.fn();
const mockDeleteWorker = vi.fn();
const mockTriggerScheduleGeneration = vi.fn();

vi.mock('../../services/api', () => ({
  getAdminWorkers: (...args: unknown[]) => mockGetAdminWorkers(...args),
  addWorker: (...args: unknown[]) => mockAddWorker(...args),
  deactivateWorker: (...args: unknown[]) => mockDeactivateWorker(...args),
  activateWorker: (...args: unknown[]) => mockActivateWorker(...args),
  setWorkerAdmin: (...args: unknown[]) => mockSetWorkerAdmin(...args),
  deleteWorker: (...args: unknown[]) => mockDeleteWorker(...args),
  triggerScheduleGeneration: (...args: unknown[]) => mockTriggerScheduleGeneration(...args),
}));

const { AdminPage } = await import('../AdminPage');

const sampleWorkers: Worker[] = [
  { id: '1', displayName: 'Alice', email: 'alice@farm.com', isActive: true, isAdmin: true },
  { id: '2', displayName: 'Bob', email: 'bob@farm.com', isActive: true, isAdmin: false },
  { id: '3', displayName: 'Charlie', email: 'charlie@farm.com', isActive: false, isAdmin: false },
];

describe('AdminPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetAdminWorkers.mockResolvedValue(sampleWorkers);
    mockAddWorker.mockResolvedValue({ id: '4', displayName: 'Dave', email: 'dave@farm.com', isActive: true, isAdmin: false });
    mockDeactivateWorker.mockResolvedValue(undefined);
    mockActivateWorker.mockResolvedValue(undefined);
    mockSetWorkerAdmin.mockResolvedValue(undefined);
    mockDeleteWorker.mockResolvedValue(undefined);
    mockTriggerScheduleGeneration.mockResolvedValue({ windowStart: '2024-01-15', windowEnd: '2024-01-28', assignments: [] });
  });

  it('shows loading state initially', () => {
    mockGetAdminWorkers.mockReturnValue(new Promise(() => {}));
    render(<AdminPage />);
    expect(screen.getByText('Loading workers...')).toBeInTheDocument();
  });

  it('renders worker table', async () => {
    render(<AdminPage />);
    expect(await screen.findByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
    expect(screen.getByText('alice@farm.com')).toBeInTheDocument();
  });

  it('renders status and role badges', async () => {
    render(<AdminPage />);
    await screen.findByText('Alice');
    const activeBadges = screen.getAllByText('Active');
    const inactiveBadges = screen.getAllByText('Inactive');
    expect(activeBadges.length).toBe(2);
    expect(inactiveBadges.length).toBe(1);
    expect(screen.getByText('Admin')).toBeInTheDocument();
  });

  it('submits add worker form', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.type(screen.getByPlaceholderText('Display name'), 'Dave');
    await user.type(screen.getByPlaceholderText('Email'), 'dave@farm.com');
    await user.click(screen.getByText('Add Worker'));

    await waitFor(() => {
      expect(mockAddWorker).toHaveBeenCalledWith('Dave', 'dave@farm.com');
    });
  });

  it('toggles active/inactive', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deactivateButtons = screen.getAllByText('Deactivate');
    await user.click(deactivateButtons[0]);

    await waitFor(() => {
      expect(mockDeactivateWorker).toHaveBeenCalledWith('1');
    });
  });

  it('toggles admin role', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const makeAdminButtons = screen.getAllByText('Make Admin');
    await user.click(makeAdminButtons[0]);

    await waitFor(() => {
      expect(mockSetWorkerAdmin).toHaveBeenCalledWith('2', true);
    });
  });

  it('deletes with confirmation', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deleteButtons = screen.getAllByText('Delete');
    await user.click(deleteButtons[0]);

    // Confirm step
    expect(screen.getByText('Confirm')).toBeInTheDocument();
    expect(screen.getByText('Cancel')).toBeInTheDocument();

    await user.click(screen.getByText('Confirm'));

    await waitFor(() => {
      expect(mockDeleteWorker).toHaveBeenCalledWith('1');
    });
  });

  it('cancels delete', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    const deleteButtons = screen.getAllByText('Delete');
    await user.click(deleteButtons[0]);
    await user.click(screen.getByText('Cancel'));

    expect(mockDeleteWorker).not.toHaveBeenCalled();
    expect(screen.queryByText('Confirm')).not.toBeInTheDocument();
  });

  it('shows error when loading fails', async () => {
    mockGetAdminWorkers.mockRejectedValue(new Error('fail'));
    render(<AdminPage />);
    expect(await screen.findByText('Failed to load workers')).toBeInTheDocument();
  });

  it('triggers schedule generation', async () => {
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.click(screen.getByText('Generate Schedule Now'));

    await waitFor(() => {
      expect(mockTriggerScheduleGeneration).toHaveBeenCalled();
    });
    expect(await screen.findByText('Schedule generated and published successfully')).toBeInTheDocument();
  });

  it('shows error when schedule generation fails', async () => {
    mockTriggerScheduleGeneration.mockRejectedValue(new Error('fail'));
    const user = userEvent.setup();
    render(<AdminPage />);
    await screen.findByText('Alice');

    await user.click(screen.getByText('Generate Schedule Now'));

    expect(await screen.findByText('Failed to generate schedule')).toBeInTheDocument();
  });
});
