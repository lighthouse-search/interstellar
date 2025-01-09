import { Roboto } from 'next/font/google';

const roboto = Roboto({
    subsets: ['latin'],
    weight: ['400', '700']
})

export default function Base(props) {
    return (
        <div className={`${props.className} ${roboto.className}`} style={props.style}>
            {props.children}
        </div>
    )
}