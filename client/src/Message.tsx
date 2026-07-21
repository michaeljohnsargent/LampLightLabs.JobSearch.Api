import React from 'react'

type MessageProps = {
    name: string,
    message: string
}

const Message: React.FC<MessageProps> = ({ name, message }  ) => {
    return(
        <p>{name}, {message}</p>
    ) 
}

export default Message