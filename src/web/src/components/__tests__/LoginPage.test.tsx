import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { LoginPage } from '../LoginPage';

describe('LoginPage', () => {
  it('renders the heading', () => {
    render(<LoginPage login={vi.fn()} />);
    expect(
      screen.getByRole('heading', { name: /farm schedule builder/i }),
    ).toBeInTheDocument();
  });

  it('renders all three login buttons', () => {
    render(<LoginPage login={vi.fn()} />);
    expect(
      screen.getByRole('button', { name: /sign in with microsoft/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /sign in with google/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /sign in with facebook/i }),
    ).toBeInTheDocument();
  });

  it('calls login with correct provider on click', async () => {
    const user = userEvent.setup();
    const login = vi.fn();
    render(<LoginPage login={login} />);

    await user.click(
      screen.getByRole('button', { name: /sign in with microsoft/i }),
    );
    expect(login).toHaveBeenCalledWith('aad');

    await user.click(
      screen.getByRole('button', { name: /sign in with google/i }),
    );
    expect(login).toHaveBeenCalledWith('google');

    await user.click(
      screen.getByRole('button', { name: /sign in with facebook/i }),
    );
    expect(login).toHaveBeenCalledWith('facebook');
  });
});
