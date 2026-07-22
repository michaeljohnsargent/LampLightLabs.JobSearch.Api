import React from 'react'

type MessageContext = {
    name: string,
    message: string
}

export const UserContext = React.createContext<MessageContext | null>(null)