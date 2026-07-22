import React from 'react'
import { UserContext } from './UserContext'


const Message: React.FC = () => {
    const userContext = React.useContext(UserContext)

    if (!userContext) {
        throw new Error('Message must be used within a UserContext.Provider')
    }

    const { name, message } = userContext

    return (
        <p>{name}, {message}</p>
    ) 
}

export default Message