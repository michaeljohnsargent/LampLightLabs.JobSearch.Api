import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import Message from './Message'
import { UserContext } from './UserContext'

describe('Message', () => {
  it('renders the name and message from context', () => {
    render(
      <UserContext.Provider value={{ name: 'Michael', message: 'Welcome back' }}>
        <Message />
      </UserContext.Provider>
    )

    expect(screen.getByText('Michael, Welcome back')).toBeInTheDocument()
  })

  it('throws when rendered outside a UserContext.Provider', () => {
    // Suppress the expected React error boundary console output for this negative test.
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<Message />)).toThrow(
      'Message must be used within a UserContext.Provider'
    )

    consoleError.mockRestore()
  })
})
